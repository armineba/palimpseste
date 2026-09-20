"""Build the reviewed, fixed-value spell recipes from distinct mechanic combinations."""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG = json.loads((ROOT / "contracts/capability-catalog.json").read_text(encoding="utf-8"))
PRIMITIVES = {item["id"]: item for item in CATALOG["effects"]}

# A recipe is a composed playable outcome, not an extra Unity effect.kind.
# Keep the French labels and the mechanic sets curated. Never multiply entries by
# changing a number, a colour, or a carrier alone.
HOSTILE = [
    ("impact", "Impact franc", "damage"),
    ("braise", "Braise persistante", "burn"),
    ("entaille", "Entaille ouverte", "bleed"),
    ("venin", "Venin lent", "poison"),
    ("gelure", "Gelure rampante", "freeze_damage"),
    ("averse", "Marque d'eau", "wet"),
    ("inertie", "Inertie imposée", "slow"),
    ("recul", "Recul brutal", "impulse"),
    ("faille", "Faille exposée", "vulnerability"),
    ("torpeur", "Torpeur offensive", "weakness"),
    ("armure_fendue", "Armure fendue", "armor_break"),
    ("soin_etrangle", "Soin étranglé", "healing_reduction"),
    ("lien_immobile", "Lien immobile", "root"),
    ("choc_neural", "Choc neural", "stun"),
    ("finisseur", "Finisseur conditionnel", "execute"),
    ("lame_incandescente", "Lame incandescente", "damage burn"),
    ("lame_ouverte", "Lame qui saigne", "damage bleed"),
    ("dard_venimeux", "Dard venimeux", "damage poison"),
    ("eclat_glacial", "Éclat glacial", "damage freeze_damage"),
    ("masse_repulsive", "Masse répulsive", "damage impulse"),
    ("frappe_pesante", "Frappe pesante", "damage slow"),
    ("pointe_fragilisante", "Pointe fragilisante", "damage vulnerability"),
    ("coup_epuisant", "Coup épuisant", "damage weakness"),
    ("perce_armure", "Perce armure", "damage armor_break"),
    ("plaie_fermee", "Plaie fermée aux soins", "damage healing_reduction"),
    ("crochet_entravant", "Crochet entravant", "damage root"),
    ("marteau_assommant", "Marteau assommant", "damage stun"),
    ("pointe_de_grace", "Pointe de grâce", "damage execute"),
    ("ponction", "Ponction vitale", "damage life_steal"),
    ("cendre_lourde", "Cendre lourde", "burn slow"),
    ("cendre_affaiblissante", "Cendre affaiblissante", "burn weakness"),
    ("cendre_exposante", "Cendre exposante", "burn vulnerability"),
    ("fusion_d_armure", "Fusion d'armure", "burn armor_break"),
    ("cautere_de_soin", "Cautère de soin", "burn healing_reduction"),
    ("souffle_ardent", "Souffle ardent", "burn impulse"),
    ("braises_entravantes", "Braises entravantes", "burn root"),
    ("saignee_lente", "Saignée lente", "bleed slow"),
    ("saignee_epuisante", "Saignée épuisante", "bleed weakness"),
    ("saignee_exposante", "Saignée exposante", "bleed vulnerability"),
    ("saignee_anti_soin", "Saignée anti soin", "bleed healing_reduction"),
    ("saignee_entravante", "Saignée entravante", "bleed root"),
    ("saignee_corrosive", "Saignée et fissure", "bleed armor_break"),
    ("venin_pesant", "Venin pesant", "poison slow"),
    ("venin_debilitant", "Venin débilitant", "poison weakness"),
    ("venin_exposant", "Venin exposant", "poison vulnerability"),
    ("venin_anti_soin", "Venin anti soin", "poison healing_reduction"),
    ("venin_entravant", "Venin entravant", "poison root"),
    ("venin_fissurant", "Venin fissurant", "poison armor_break"),
    ("givre_pesant", "Givre pesant", "freeze_damage slow"),
    ("givre_immobilisant", "Givre immobilisant", "freeze_damage root"),
    ("givre_assommant", "Givre assommant", "freeze_damage stun"),
    ("givre_faible", "Givre affaiblissant", "freeze_damage weakness"),
    ("givre_exposant", "Givre exposant", "freeze_damage vulnerability"),
    ("projection_liante", "Projection liante", "impulse root"),
    ("projection_siderante", "Projection sidérante", "impulse stun"),
    ("projection_exposante", "Projection exposante", "impulse vulnerability"),
    ("eau_lourde", "Eau lourde", "wet slow"),
    ("eau_liante", "Eau liante", "wet root"),
    ("lave_visqueuse", "Lave visqueuse", "damage burn slow"),
    ("lave_fissurante", "Lave fissurante", "damage burn vulnerability"),
    ("lave_epuisante", "Lave épuisante", "damage burn weakness"),
    ("lave_corrosive", "Lave corrosive", "damage burn armor_break"),
    ("lave_cauterisante", "Lave cautérisante", "damage burn healing_reduction"),
    ("eruption_repulsive", "Éruption répulsive", "damage burn impulse"),
    ("lave_entravante", "Lave entravante", "damage burn root"),
    ("lave_assommante", "Lave assommante", "damage burn stun"),
    ("braise_de_grace", "Braise de grâce", "damage burn execute"),
    ("estoc_saignant_lent", "Estoc saignant et lourd", "damage bleed slow"),
    ("estoc_saignant_fragile", "Estoc saignant et exposant", "damage bleed vulnerability"),
    ("estoc_saignant_faible", "Estoc saignant et épuisant", "damage bleed weakness"),
    ("estoc_saignant_armure", "Estoc saignant et perforant", "damage bleed armor_break"),
    ("estoc_saignant_soin", "Estoc saignant et stérile", "damage bleed healing_reduction"),
    ("estoc_saignant_recul", "Estoc saignant et repoussant", "damage bleed impulse"),
    ("estoc_saignant_lien", "Estoc saignant et liant", "damage bleed root"),
    ("estoc_saignant_choc", "Estoc saignant et sidérant", "damage bleed stun"),
    ("estoc_saignant_fin", "Estoc saignant de grâce", "damage bleed execute"),
    ("dard_venimeux_lent", "Dard venimeux et pesant", "damage poison slow"),
    ("dard_venimeux_fragile", "Dard venimeux et exposant", "damage poison vulnerability"),
    ("dard_venimeux_faible", "Dard venimeux et épuisant", "damage poison weakness"),
    ("dard_venimeux_armure", "Dard venimeux et perforant", "damage poison armor_break"),
    ("dard_venimeux_soin", "Dard venimeux et stérile", "damage poison healing_reduction"),
    ("dard_venimeux_recul", "Dard venimeux et repoussant", "damage poison impulse"),
    ("dard_venimeux_lien", "Dard venimeux et liant", "damage poison root"),
    ("dard_venimeux_choc", "Dard venimeux et sidérant", "damage poison stun"),
    ("dard_venimeux_fin", "Dard venimeux de grâce", "damage poison execute"),
    ("eclat_givre_lent", "Éclat de givre pesant", "damage freeze_damage slow"),
    ("eclat_givre_fragile", "Éclat de givre exposant", "damage freeze_damage vulnerability"),
    ("eclat_givre_faible", "Éclat de givre épuisant", "damage freeze_damage weakness"),
    ("eclat_givre_armure", "Éclat de givre perforant", "damage freeze_damage armor_break"),
    ("eclat_givre_recul", "Éclat de givre repoussant", "damage freeze_damage impulse"),
    ("eclat_givre_lien", "Éclat de givre liant", "damage freeze_damage root"),
    ("eclat_givre_choc", "Éclat de givre sidérant", "damage freeze_damage stun"),
    ("eclat_givre_fin", "Éclat de givre de grâce", "damage freeze_damage execute"),
    ("lave_lourde_exposante", "Lave lourde et exposante", "damage burn slow vulnerability"),
    ("lave_recul_perforant", "Lave répulsive et perforante", "damage burn impulse armor_break"),
    ("lave_liante_sterile", "Lave liante et stérile", "damage burn root healing_reduction"),
    ("entaille_lourde_epuisante", "Entaille lourde et épuisante", "damage bleed slow weakness"),
    ("entaille_recul_expose", "Entaille répulsive et exposante", "damage bleed impulse vulnerability"),
    ("entaille_liante_sterile", "Entaille liante et stérile", "damage bleed root healing_reduction"),
    ("venin_lourd_sterile", "Venin lourd et stérile", "damage poison slow healing_reduction"),
    ("venin_epuisant_expose", "Venin épuisant et exposant", "damage poison weakness vulnerability"),
    ("givre_lourd_liant", "Givre lourd et liant", "damage freeze_damage slow root"),
    ("givre_recul_siderant", "Givre répulsif et sidérant", "damage freeze_damage impulse stun"),
    ("ponction_sanglante", "Ponction sanglante", "damage life_steal bleed"),
    ("ponction_ardente", "Ponction ardente", "damage life_steal burn"),
    ("ponction_venimeuse", "Ponction venimeuse", "damage life_steal poison"),
    ("ponction_glaciale", "Ponction glaciale", "damage life_steal freeze_damage"),
    ("ponction_exposante", "Ponction exposante", "damage life_steal vulnerability"),
    ("ponction_epuisante", "Ponction épuisante", "damage life_steal weakness"),
    ("ponction_perforante", "Ponction perforante", "damage life_steal armor_break"),
    ("ponction_pesante", "Ponction pesante", "damage life_steal slow"),
    ("fin_sanglante", "Fin sanglante", "execute bleed"),
    ("fin_venimeuse", "Fin venimeuse", "execute poison"),
    ("fin_exposante", "Fin exposante", "execute vulnerability"),
    ("fin_perforante", "Fin perforante", "execute armor_break"),
    ("dissipation", "Dissipation hostile", "dispel"),
    ("impact_dissipant", "Impact dissipant", "damage dispel"),
    ("dissipation_exposante", "Dissipation exposante", "dispel vulnerability"),
    ("dissipation_siderante", "Dissipation sidérante", "dispel stun"),
]

SUPPORT = [
    ("soin", "Soin immédiat", "heal"),
    ("regain", "Regain progressif", "regen"),
    ("egide", "Égide de santé", "barrier_health"),
    ("resistance", "Résistance temporaire", "damage_reduction"),
    ("elan", "Élan de mouvement", "haste"),
    ("purge", "Purge des afflictions", "cleanse"),
    ("soin_durable", "Soin durable", "heal regen"),
    ("soin_protege", "Soin protégé", "heal barrier_health"),
    ("soin_resistant", "Soin et résistance", "heal damage_reduction"),
    ("soin_mobile", "Soin et élan", "heal haste"),
    ("soin_purifiant", "Soin purifiant", "heal cleanse"),
    ("regain_protege", "Regain protégé", "regen barrier_health"),
    ("regain_resistant", "Regain et résistance", "regen damage_reduction"),
    ("regain_mobile", "Regain et élan", "regen haste"),
    ("egide_resistante", "Égide résistante", "barrier_health damage_reduction"),
    ("egide_mobile", "Égide mobile", "barrier_health haste"),
    ("purge_regenerante", "Purge régénérante", "cleanse regen"),
    ("purge_resistante", "Purge résistante", "cleanse damage_reduction"),
    ("benediction_complete", "Bénédiction complète", "heal regen barrier_health"),
    ("restauration_renforcee", "Restauration renforcée", "heal regen damage_reduction"),
    ("secours_purifiant", "Secours purifiant", "heal cleanse barrier_health"),
]

ENVIRONMENT = [
    ("fracas_de_caisse", "Fracas de caisse", "shatter"),
]

MEANINGS = {
    "damage": "retire immédiatement de la santé",
    "heal": "rend immédiatement de la santé",
    "impulse": "repousse physiquement la cible",
    "burn": "inflige une brûlure périodique",
    "wet": "applique Mouillé, qui éteint ou empêche la brûlure",
    "slow": "réduit temporairement la vitesse",
    "bleed": "inflige une hémorragie périodique",
    "poison": "inflige un poison périodique",
    "freeze_damage": "inflige une gelure périodique sans immobilisation automatique",
    "regen": "restaure périodiquement la santé",
    "barrier_health": "accorde une réserve de santé absorbante",
    "vulnerability": "augmente temporairement les dégâts reçus",
    "weakness": "réduit temporairement les dégâts infligés",
    "haste": "augmente temporairement la vitesse de mouvement",
    "armor_break": "retire une partie de la réduction active de dégâts",
    "damage_reduction": "réduit temporairement les dégâts reçus",
    "healing_reduction": "réduit temporairement les soins reçus",
    "root": "immobilise brièvement sans supprimer les dégâts",
    "stun": "interrompt brièvement les actions",
    "cleanse": "retire les altérations nettoyables",
    "dispel": "retire les effets positifs dissipables de l'ennemi",
    "life_steal": "soigne le lanceur d'une fraction des dégâts effectivement infligés par ce même impact",
    "execute": "ajoute des dégâts bornés seulement sous 25 % de santé",
    "shatter": "endommage la structure d'une caisse du labo marquée destructible",
}

AMOUNTS = {
    "damage": 12000, "heal": 15000, "impulse": 18000,
    "burn": 1400, "wet": 0, "slow": 260, "bleed": 1400,
    "poison": 1200, "freeze_damage": 1200, "regen": 1500,
    "barrier_health": 15000, "vulnerability": 180, "weakness": 180,
    "haste": 200, "armor_break": 180, "damage_reduction": 180,
    "healing_reduction": 220, "root": 0, "stun": 0,
    "cleanse": 0, "dispel": 0, "life_steal": 250,
    "execute": 7000, "shatter": 18000,
}

INSTANT = {"damage", "heal", "impulse", "cleanse", "dispel", "life_steal", "execute", "shatter"}
SHORT = {"root": 60, "stun": 35}
HOSTILE_CARRIERS = ["projectile", "beam", "pulse", "field", "trap"]
SUPPORT_CARRIERS = ["projectile", "beam", "pulse", "field", "trap"]
ENV_CARRIERS = ["projectile", "beam", "pulse", "field", "trap"]


def make_recipe(row, family, target, carriers):
    key, label, signature = row
    kinds = signature.split()
    components = []
    for kind in kinds:
        if kind not in PRIMITIVES:
            raise ValueError(f"Unknown primitive: {kind}")
        component = {
            "kind": kind,
            "amount": AMOUNTS[kind],
            "duration_ticks": 0 if kind in INSTANT else SHORT.get(kind, 150),
            "direction": "outward" if kind == "impulse" else "none",
        }
        primitive = PRIMITIVES[kind]
        if not primitive["min_amount"] <= component["amount"] <= primitive["max_amount"]:
            raise ValueError(f"Amount outside catalog: {kind}")
        components.append(component)
    if "life_steal" in kinds and "damage" not in kinds:
        raise ValueError(f"Life steal without same-event damage: {key}")
    if "burn" in kinds and "wet" in kinds:
        raise ValueError(f"Self-cancelling burn/wet recipe: {key}")
    if "shatter" in kinds and target != "environment":
        raise ValueError(f"Shatter without breakable receiver: {key}")
    if len(kinds) > 8:
        raise ValueError(f"Recipe exceeds effect cap: {key}")
    sentence = "; ".join(MEANINGS[kind] for kind in kinds)
    return {
        "id": f"r_{key}",
        "label_fr": label,
        "family": family,
        "description_fr": sentence[0].upper() + sentence[1:] + ".",
        "allowed_carriers": carriers,
        "target_filter": target,
        "components": components,
    }


recipes = (
    [make_recipe(row, "hostile", "hostile", HOSTILE_CARRIERS) for row in HOSTILE]
    + [make_recipe(row, "support", "ally", SUPPORT_CARRIERS) for row in SUPPORT]
    + [make_recipe(row, "environment", "environment", ENV_CARRIERS) for row in ENVIRONMENT]
)
ids = [r["id"] for r in recipes]
signatures = [r["target_filter"] + ":" + ",".join(sorted(c["kind"] for c in r["components"])) for r in recipes]
if len(ids) != len(set(ids)) or len(signatures) != len(set(signatures)):
    raise ValueError("Duplicate recipe ID or mechanical signature")
if len(recipes) < 100:
    raise ValueError(f"Only {len(recipes)} recipes")

out = {
    "schema_version": "sp.effect-recipes/1.0",
    "production_catalog": "capability-catalog.json",
    "semantics": "Each recipe expands to its fixed, ordered, bounded SpellEffect components on one target and one carrier event. It never executes generated code.",
    "recipes": recipes,
}
destination = ROOT / "contracts/effect-recipes.json"
destination.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

def short_hint(description: str, limit: int = 70) -> str:
    """Keep only existing approved meaning, ending at a word boundary."""
    if len(description) <= limit:
        return description
    return description[: limit - 1].rsplit(" ", 1)[0].rstrip(" ;,.") + "…"

projection = {
    "schema_version": "sp.effect-recipes-prompt/1.0",
    "source": "effect-recipes.json",
    "allowed_carriers": HOSTILE_CARRIERS,
    "event_by_carrier": {
        "projectile": "hit", "beam": "hit", "pulse": "hit",
        "field": "enter", "trap": "trigger",
    },
    "recipes": [
        {
            "id": recipe["id"],
            "label_fr": recipe["label_fr"],
            "hint_fr": short_hint(recipe["description_fr"]),
            "target_filter": recipe["target_filter"],
            "kinds": [component["kind"] for component in recipe["components"]],
        }
        for recipe in recipes
    ],
}
projection_path = ROOT / "contracts/effect-recipes-prompt.json"
projection_path.write_text(json.dumps(projection, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
if projection_path.stat().st_size > 30000:
    raise ValueError("Model-facing projection exceeds 30 KB")
print(f"{len(recipes)} unique playable recipes in {destination}")
print(f"Model-facing projection: {projection_path.stat().st_size} bytes")
