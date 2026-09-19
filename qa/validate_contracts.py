#!/usr/bin/env python3
"""Contrôles DOCUMENTAIRES SP-1.0. Aucun appel modèle, moteur Unity ou test physique.
Usage: python qa/validate_contracts.py  (depuis n'importe quel répertoire)
Dépendances: pip install -r qa/requirements.txt
"""
from __future__ import annotations
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import json
import sys
from datetime import datetime, timezone
import jsonschema
import yaml

ROOT = Path(__file__).resolve().parents[1]
C, E = ROOT / 'contracts', ROOT / 'examples'
REPORT: list[dict] = []

def load(p: Path):
    return json.loads(p.read_text(encoding='utf-8'))

def check(name, action, *, rejection=False):
    try:
        action()
        ok = not rejection
        detail = 'Accepté' if ok else 'Erreur attendue non détectée'
    except (ValueError, AssertionError, KeyError, jsonschema.ValidationError) as ex:
        ok = rejection
        detail = (('Rejet attendu : ' if ok else 'Échec : ') + str(ex))[:700]
    REPORT.append({'name': name, 'passed': ok, 'detail': detail})

def ensure(condition: bool, text: str):
    if not condition:
        raise ValueError(text)

SCHEMAS = {p.name: load(p) for p in C.glob('*.schema.json')}
CAT = load(C / 'capability-catalog.json')
CARRIERS = {c['id']: c for c in CAT['carriers']}
EFFECTS = {e['id']: e for e in CAT['effects']}
GEO = {g['geometry_id']: g for p in (E / 'geometry').glob('*.json') if (g := load(p))}

def validate(obj, schema_name):
    jsonschema.Draft202012Validator(SCHEMAS[schema_name], format_checker=jsonschema.FormatChecker()).validate(obj)

def description_domain(d):
    validate(d, 'spell-description.schema.json')
    obs = {x['id'] for x in d['observations']}
    clauses = {x['id'] for x in d['clauses']}
    ensure(len(obs) == len(d['observations']), 'Observation dupliquée')
    ensure(len(clauses) == len(d['clauses']), 'Clause dupliquée')
    subjects = {x['subject_id'] for x in d['clauses'] if x['kind'] == 'mechanical'}
    for x in d['clauses']:
        ensure(set(x['observation_ids']) <= obs, 'Observation inconnue')
        if x['kind'] == 'visual_only':
            ensure(not any(f['dimension'] == 'effect' for f in x['facts']), 'Effet sous clause purement visuelle')
    incoming = {}
    for r in d['relations']:
        ensure(r['source_subject_id'] in subjects and r['target_subject_id'] in subjects, 'Sujet de relation inconnu')
        ensure(r['target_subject_id'] not in incoming, 'Plusieurs parents de sujet')
        ensure(set(r['clause_ids']) <= clauses, 'Clause de relation inconnue')
        incoming[r['target_subject_id']] = r['source_subject_id']
    for sid in subjects:
        visited = set()
        while sid in incoming:
            ensure(sid not in visited, 'Cycle de sujets')
            visited.add(sid)
            sid = incoming[sid]
    for r in d['shape_requests']:
        ensure(r['subject_id'] in subjects, 'Sujet géométrique inconnu')
    for sid in subjects:
        carriers = {f['value'] for c in d['clauses'] if c['subject_id'] == sid for f in c['facts'] if f['dimension'] == 'carrier'}
        ensure(len(carriers) == 1, 'Un porteur principal par sujet mécanique')

def plan_domain(p, *, description=None, geometry=True):
    """Sous-ensemble explicite des invariants, PAS le futur compilateur pessimiste."""
    validate(p, 'spell-plan.schema.json')
    nodes = {n['node_id']: n for n in p['nodes']}
    ensure(len(nodes) == len(p['nodes']), 'Identifiant de nœud dupliqué')
    ensure(len({n['subject_id'] for n in p['nodes']}) == len(nodes), 'Sujet de plan dupliqué')
    instances = 0
    for n in nodes.values():
        a, o, c = n['activation'], n['options'], n['carrier']
        instances += a['copies'] * a['max_activations']
        ensure(a['copies'] != 1 or a['spread_mdeg'] == 0, 'Éventail sur copie unique')
        if a['parent_id'] is None:
            ensure(a['event'] == 'cast' and a['max_activations'] == 1 and n['anchor'] != 'parent_event', 'Racine invalide')
        else:
            ensure(a['parent_id'] in nodes, 'Parent inconnu')
            ensure(a['event'] in CARRIERS[nodes[a['parent_id']]['carrier']]['emits'], 'Événement non émis par le parent')
            ensure(n['anchor'] == 'parent_event', 'Ancrage enfant invalide')
        seen, cur = set(), n
        while cur['activation']['parent_id'] is not None:
            ensure(cur['node_id'] not in seen, 'Cycle de nœuds')
            seen.add(cur['node_id'])
            cur = nodes[cur['activation']['parent_id']]
        ensure(len({e['id'] for e in n['effects']}) == len(n['effects']), 'Effet dupliqué dans son nœud')
        for e in n['effects']:
            rule = EFFECTS[e['kind']]
            ensure(e['event'] in CARRIERS[c]['effect_events'], 'Événement d’effet incompatible')
            ensure(e['amount'] <= rule['max_amount'], 'Quantité hors profil')
            if rule['duration_rule'] == 'zero':
                ensure(e['duration_ticks'] == 0, 'Durée d’un effet instantané')
            else:
                ensure(e['duration_ticks'] > 0, 'Statut sans durée')
                if rule['duration_rule'] == 'positive_multiple_of_50':
                    ensure(e['duration_ticks'] % 50 == 0, 'Cadence de brûlure invalide')
            ensure((e['direction'] != 'none') == (e['kind'] == 'impulse'), 'Direction incompatible')
        if c == 'projectile':
            ensure(o['motion'] == 'homing' or o['turn_mdeg_s'] == 0, 'Guidage sans mode guidé')
        elif c == 'beam':
            ensure(o['lifetime_ticks'] == 1 or o['tick_interval'] >= 5, 'Cadence faisceau trop courte')
            ensure((o['chain_hops'] == 0) == (o['chain_radius_cm'] == 0), 'Rayon de chaîne incohérent')
        elif c == 'trap':
            ensure(o['arm_ticks'] < o['lifetime_ticks'], 'Armement après expiration')
        elif c == 'pulse':
            ensure(o['front_width_cm'] <= o['radius_cm'], 'Front plus large que rayon')
        if geometry:
            ensure(n['geometry_id'] in GEO, 'Géométrie inconnue')
            ensure(n['appearance']['signature_geometry_id'] in GEO, 'Signature inconnue')
            if c == 'barrier' or (c == 'projectile' and o['motion'] == 'curve'):
                ensure(GEO[n['geometry_id']]['kind'] == 'path', 'Chemin requis')
    ensure(instances <= 128, 'Borne globale d’instances dépassée')
    if description is not None:
        description_domain(description)
        clauses = {c['id']: c for c in description['clauses']}
        subjects = {c['subject_id'] for c in description['clauses'] if c['kind'] == 'mechanical'}
        ensure(subjects == {n['subject_id'] for n in nodes.values()}, 'Sujets perdus ou ajoutés')
        mapped = {n['subject_id']: n for n in nodes.values()}
        relations = {r['target_subject_id']: r for r in description['relations']}
        for sid, n in mapped.items():
            ensure(set(n['clause_ids']) <= set(clauses), 'Clause de nœud inconnue')
            for e in n['effects']:
                ensure(set(e['clause_ids']) <= set(clauses), 'Clause d’effet inconnue')
                ensure(all(clauses[c]['subject_id'] == sid for c in e['clause_ids']), 'Effet justifié par un autre sujet')
            facts = [(f['dimension'], f['value']) for c in description['clauses'] if c['subject_id'] == sid for f in c['facts']]
            expected_effects = {v for k,v in facts if k == 'effect'}
            ensure(expected_effects == {e['kind'] for e in n['effects']}, 'Effet ajouté ou supprimé')
            for dim,val in facts:
                actual = {'carrier': {n['carrier']}, 'target': {e['target_filter'] for e in n['effects']},
                          'event': {e['event'] for e in n['effects']}, 'effect': {e['kind'] for e in n['effects']},
                          'affinity': {n['appearance']['affinity']}, 'pattern': {n['appearance']['pattern']},
                          'motion': {n['options'].get('motion', 'expanding' if n['carrier']=='pulse' else 'stationary')}}
                ensure(val in actual[dim], 'Fait non conservé : '+dim+'='+val)
            r = relations.get(sid)
            if r:
                a = n['activation']
                ensure(a['parent_id'] == mapped[r['source_subject_id']]['node_id'] and a['event'] == r['event'] and a['max_activations'] == r['max_activations'], 'Relation non conservée')
            else:
                ensure(n['activation']['parent_id'] is None, 'Relation ajoutée')
            if geometry:
                g = GEO[n['geometry_id']]
                ensure(any(r['subject_id']==sid and r['region']==g['source_region'] and r['role']==g['kind'] for r in description['shape_requests']), 'Région/rôle géométrique non conservé')

def geometry_domain(g):
    validate(g, 'geometry.schema.json')
    if g['kind'] == 'path':
        ensure(len({(p['x'],p['z']) for p in g['points']}) >= 2, 'Chemin dégénéré')
    if g['kind'] == 'footprint':
        ensure(g['mask_file'] is not None, 'Masque d’empreinte manquant')
    if g['mask_file']:
        ensure('/' not in g['mask_file'] and '\\' not in g['mask_file'] and '..' not in g['mask_file'], 'Nom de masque dangereux')
        ensure((E / 'geometry' / g['mask_file']).is_file(), 'Fichier masque absent')

def packet_integrity(packet, desc, plan):
    validate(packet, 'compiled-spell.schema.json')
    ensure(packet['plan'] == plan, 'Plan embarqué divergent')
    ensure(packet['description_sha256'] == plan['description_sha256'] == sha256((E/'01_description_illustrative.json').read_bytes()).hexdigest(), 'Empreinte description divergente')
    names = {a['file_name'] for a in packet['binary_assets']}
    ensure(len(names)==len(packet['binary_assets']), 'Nom de masque dupliqué')
    total = 0
    for g in packet['geometry_manifest']:
        data = (E/'geometry'/(g['id']+'.json')).read_bytes()
        ensure(sha256(data).hexdigest()==g['sha256'] and len(data)==g['size_bytes'], 'Intégrité JSON géométrique')
        total += len(data)
        mask = GEO[g['id']]['mask_file']
        ensure(mask is None or mask in names, 'Masque non manifesté')
    for a in packet['binary_assets']:
        data = (E/'geometry'/a['file_name']).read_bytes()
        ensure(sha256(data).hexdigest()==a['sha256'] and len(data)==a['size_bytes'], 'Intégrité masque binaire')
        total += len(data)
    ensure(total==packet['resource_bounds']['geometry_bytes'], 'Poids géométrique inexact')
    ensure(packet['resource_bounds']['max_end_tick']>=476, 'Statut résiduel oublié dans la fixture')
    plan_domain(plan, description=desc)

def api_structure():
    """Contrôle structurel des références, pas une validation OpenAPI exhaustive."""
    data = yaml.safe_load((C/'openapi.yaml').read_text())
    ensure(data['openapi']=='3.1.0', 'Version API')
    ids = [op['operationId'] for path in data['paths'].values() for method,op in path.items() if method in ('get','put','post','delete','patch')]
    ensure(len(ids)==len(set(ids)), 'operationId dupliqué')
    def visit(node):
        if isinstance(node, dict):
            if '$ref' in node:
                ref = node['$ref']; file,_,pointer = ref.partition('#')
                target = load(C/file) if file else data
                for part in pointer.strip('/').split('/') if pointer else []:
                    target = target[part.replace('~1','/').replace('~0','~')]
            for val in node.values(): visit(val)
        elif isinstance(node,list):
            for val in node: visit(val)
    visit(data)
    ensure(len(ids)==16, 'Nombre d’opérations API modifié ; mettre à jour le contrôle explicite')

D=load(E/'01_description_illustrative.json');P=load(E/'02_plan_illustratif.json');PACK=load(E/'03_paquet_illustratif.json')
for name,schema in SCHEMAS.items():
    check('Schéma valide : '+name, lambda s=schema: jsonschema.Draft202012Validator.check_schema(s))
check('Description : forme et références', lambda: description_domain(D))
check('Plan : forme, domaines et traçabilité', lambda: plan_domain(P, description=D))
for path in sorted(E.glob('fixture_*.json')):
    check('Fixture porteur : '+path.stem, lambda p=path: plan_domain(load(p)))
for gid,g in GEO.items():
    check('Géométrie : '+gid, lambda g=g: geometry_domain(g))
check('Paquet : schéma, intégrité, masques et bornes illustratives', lambda: packet_integrity(PACK,D,P))
check('OpenAPI : références résolubles et opérations uniques', api_structure)
for name, target in [('model-a','spell-description.schema.json'),('model-b','spell-plan.schema.json')]:
    fmt=load(C/(name+'.response-format.json'))
    check('Format local fournisseur : '+name, lambda f=fmt: jsonschema.Draft202012Validator.check_schema(f['schema']))
    check('Exemple conforme au format local : '+name, lambda f=fmt,d=D if name=='model-a' else P: jsonschema.Draft202012Validator(f['schema']).validate(d))

# Mutations rejetées : vérification de l’échec, pas seulement des cas heureux.
def mutation(name, base, edit, validator):
    def run():
        data=deepcopy(base); edit(data); validator(data)
    check('Rejet : '+name, run, rejection=True)
mutation('Champ code arbitraire',P,lambda p:p.update(code='RunAnything()'),plan_domain)
mutation('Porteur inconnu',P,lambda p:p['nodes'][0].update(carrier='summon_dragon'),plan_domain)
mutation('Vitesse nulle',P,lambda p:p['nodes'][0]['options'].update(speed_cm_s=0),plan_domain)
mutation('Valeur NaN',P,lambda p:p['nodes'][0]['options'].update(speed_cm_s=float('nan')),plan_domain)
mutation('Dégât excessif',P,lambda p:p['nodes'][0]['effects'][0].update(amount=100001),plan_domain)
mutation('Événement direct impossible',P,lambda p:p['nodes'][0]['effects'][0].update(event='expire'),plan_domain)
mutation('Durée sur dommage direct',P,lambda p:p['nodes'][0]['effects'][0].update(duration_ticks=1),plan_domain)
mutation('Brûlure sans durée',P,lambda p:p['nodes'][1]['effects'][0].update(duration_ticks=0),plan_domain)
mutation('Brûlure fractionnée',P,lambda p:p['nodes'][1]['effects'][0].update(duration_ticks=149),plan_domain)
mutation('Direction sur dommage',P,lambda p:p['nodes'][0]['effects'][0].update(direction='up'),plan_domain)
mutation('Parent inexistant',P,lambda p:p['nodes'][1]['activation'].update(parent_id='missing'),plan_domain)
mutation('Auto-cycle',P,lambda p:p['nodes'][1]['activation'].update(parent_id='f1'),plan_domain)
mutation('Copie unique avec éventail',P,lambda p:p['nodes'][0]['activation'].update(spread_mdeg=100),plan_domain)
mutation('Racine répétée',P,lambda p:p['nodes'][0]['activation'].update(max_activations=2),plan_domain)
mutation('Guidage sur mouvement courbe',P,lambda p:p['nodes'][0]['options'].update(turn_mdeg_s=90000),plan_domain)
mutation('Géométrie absente',P,lambda p:p['nodes'][0].update(geometry_id='missing'),plan_domain)
mutation('Courbe sans chemin',P,lambda p:p['nodes'][0].update(geometry_id='outer.footprint.0'),plan_domain)
mutation('Fait remplacé sans autorisation',P,lambda p:p['nodes'][0]['effects'][0].update(kind='heal'),lambda p:plan_domain(p,description=D))
mutation('Cible remplacée sans autorisation',P,lambda p:p['nodes'][0]['effects'][0].update(target_filter='ally'),lambda p:plan_domain(p,description=D))
mutation('Relation modifiée',P,lambda p:p['nodes'][1]['activation'].update(max_activations=2),lambda p:plan_domain(p,description=D))
mutation('Observation inconnue',D,lambda d:d['clauses'][0].update(observation_ids=['missing']),description_domain)
mutation('Observation dupliquée',D,lambda d:d['observations'][1].update(id='o1'),description_domain)
mutation('Relation vers sujet absent',D,lambda d:d['relations'][0].update(target_subject_id='missing'),description_domain)
mutation('Chemin dégénéré',GEO['ring.path.0'],lambda g:g.update(points=[{'x':0,'z':0}]),geometry_domain)
mutation('Chemin de fichier dangereux',GEO['outer.footprint.0'],lambda g:g.update(mask_file='../secret.png'),geometry_domain)
mutation('Empreinte sans masque',GEO['outer.footprint.0'],lambda g:g.update(mask_file=None),geometry_domain)
mutation('Masque absent du manifeste',PACK,lambda p:p.update(binary_assets=[]),lambda p:packet_integrity(p,D,P))
mutation('Empreinte JSON corrompue',PACK,lambda p:p['geometry_manifest'][0].update(sha256='0'*64),lambda p:packet_integrity(p,D,P))
mutation('Empreinte PNG corrompue',PACK,lambda p:p['binary_assets'][0].update(sha256='0'*64),lambda p:packet_integrity(p,D,P))
mutation('Durée oubliant les statuts',PACK,lambda p:p['resource_bounds'].update(max_end_tick=326),lambda p:packet_integrity(p,D,P))
for carrier, name, edit in [
 ('trap','Piège armé après extinction',lambda p:p['nodes'][0]['options'].update(arm_ticks=300)),
 ('beam','Chaîne sans rayon',lambda p:p['nodes'][0]['options'].update(chain_radius_cm=0)),
 ('beam','Cadence faisceau trop courte',lambda p:p['nodes'][0]['options'].update(tick_interval=1)),
 ('pulse','Front plus large que portée',lambda p:p['nodes'][0]['options'].update(radius_cm=30)),
 ('field','Mouillé avec quantité',lambda p:p['nodes'][0]['effects'][0].update(amount=1)),
 ('projectile','Impulsion sans direction',lambda p:p['nodes'][0]['effects'][0].update(direction='none'))]:
    mutation(name,load(E/f'fixture_{carrier}.json'),edit,plan_domain)

result = {'scope':'DOCUMENT_CONTRACTS_ONLY_NOT_UNITY_NOT_PROVIDER_NOT_HUMAN_ACCEPTANCE',
          'generated_at':datetime.now(timezone.utc).isoformat(), 'total':len(REPORT),
          'passed':sum(x['passed'] for x in REPORT), 'failed':sum(not x['passed'] for x in REPORT),
          'checks':REPORT,
          'not_tested':['Appels API réels','Compatibilité fournisseur du schéma strict','Compilation C# / IL2CPP','Résolveur raster réel','Calcul pessimiste complet','Physics Unity','Rendu et audio','Avis humain']}
out=ROOT/'qa/rapport_controles_documentaires.json'
out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f"{result['passed']}/{result['total']} contrôles documentaires réussis ; {result['failed']} échecs.")
for row in REPORT:
    if not row['passed']: print('FAIL',row['name'],row['detail'])
print('Aucun appel modèle ou test Unity exécuté.')
sys.exit(1 if result['failed'] else 0)
