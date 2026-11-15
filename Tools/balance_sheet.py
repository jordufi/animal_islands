# Hoja de balance teórica de Animal Island (Fase 2 del plan).
# Lee los .asset de Assets/Data y saca, por ronda: abuelos, vida, conchas por arma y tiempo para matar (TTK).
# Uso:  python Tools/balance_sheet.py            (desde la raíz del proyecto)
# Supuestos: un jugador, dificultad Normal, sin cartas, todos los tiros al cuerpo y ninguno fallado,
# abuelos normales (sin variantes; la noche = senderistas). Las fórmulas de ronda copian RoundManager.
import os, re, math

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Data")

def asset(path):
    out = {}
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^  (\w+): (.+?)\s*$", line)
            if m:
                v = m.group(2)
                try: out[m.group(1)] = float(v)
                except ValueError: out[m.group(1)] = v
    return out

eco = asset("Economy.asset")
eco.setdefault("fastHitPoints", 5.0); eco.setdefault("fastFireRate", 8.0)
eco.setdefault("bossHealthInEnemies", 45.0); eco.setdefault("bossMinHealth", 30000.0)
weapons = {n: asset("Weapons/%s.asset" % n) for n in ("Pistol", "Shotgun", "LentosPeta", "SMG", "Sniper")}
uku = asset("Weapons/Ukulele.asset")
night = asset("Enemies/AbueloSenderista.asset")

def health(r):            # RoundManager.BaseHealth
    return 150 + 100 * (r - 1) if r <= 9 else (150 + 100 * 8) * 1.1 ** (r - 9)

def count(r):             # RoundManager: 6 + 2.2r + 0.08r², noche x0.6
    n = 6 + 2.2 * r + 0.08 * r * r
    return int(round(n * (0.6 if r % 5 == 0 else 1)))

def shot_damage(w, upgraded):
    return w["damage"] * w["pellets"] * (w["upgradeDamageMultiplier"] if upgraded else 1)

def hit_pay(w):
    return eco["fastHitPoints"] if 1 / w["fireInterval"] > eco["fastFireRate"] else eco["hitPoints"]

def per_enemy(w, hp, upgraded, mult):
    shots = math.ceil(hp / shot_damage(w, upgraded))
    return shots, (shots - 1) * hit_pay(w) + round(eco["killPoints"] * mult), (shots - 1) * w["fireInterval"]

print("Economía: impacto %d (armas rápidas %d), baja %d, cabeza %d, ukelele %d" %
      (eco["hitPoints"], eco["fastHitPoints"], eco["killPoints"], eco["headshotKillPoints"], eco["meleeKillPoints"]))
print()
names = {"Pistol": "Pistola", "Shotgun": "Chancla", "LentosPeta": "Lentos", "SMG": "Thermomix", "Sniper": "Catalejo"}
print("Conchas por ronda si se mata todo con un arma (tiros | TTK s | conchas de la ronda); * = arma mejorada")
print("%-5s %-4s %-6s" % ("ronda", "n", "vida") + "".join("%-22s" % names[k] for k in weapons) + "Ukelele")
for r in (1, 2, 3, 4, 5, 7, 9, 10, 12, 14, 16):
    is_night = r % 5 == 0
    hp = health(r) * (night["healthMultiplier"] if is_night else 1)
    mult = night["pointsMultiplier"] if is_night else 1
    n = count(r)
    row = "%-5s %-4d %-6d" % (str(r) + ("n" if is_night else ""), n, hp)
    up = r >= 12
    for k, w in weapons.items():
        shots, pay, ttk = per_enemy(w, hp, up, mult)
        row += "%-22s" % ("%s%d | %.1f | %d" % ("*" if up else "", shots, ttk, pay * n))
    hits = math.ceil(hp / uku["damage"])
    row += "%d golpes | %d" % (hits, n * ((hits - 1) * eco["hitPoints"] + round(eco["meleeKillPoints"] * mult)))
    print(row)

print()
print("Jefe: vida = max(%d, %d x vida del abuelo de la ronda)" % (eco["bossMinHealth"], eco["bossHealthInEnemies"]))
for r in (6, 8, 10, 12, 14, 16):
    hp = max(eco["bossMinHealth"], eco["bossHealthInEnemies"] * health(r))
    row = "  ronda %-3d vida %-7d" % (r, hp)
    for k in ("SMG", "Shotgun", "Sniper"):
        w = weapons[k]
        mag = w["magazineSize"] * w["upgradeMagazineMultiplier"]
        dps = shot_damage(w, True) / w["fireInterval"]
        sustained = shot_damage(w, True) * mag / (mag * w["fireInterval"] + w["reloadTime"])
        ammo = shot_damage(w, True) * (mag + w["maxReserve"])
        row += " | %s*: %.0f s sin fallar, daño de toda la munición %.0f %%" % (names[k], hp / sustained, 100 * ammo / hp)
    print(row)
