"""Village v2 layout in world units (100 px = 1 unit). Origin = map centre, +y up.
Shared by the ground painter and mirrored in TowerBootstrap.BuildVillage (keep in sync)."""

HALF_W, HALF_H = 18.0, 10.0

# upper terrace (tower plateau): walkable top is y >= CLIFF_TOP between x in [-PLATEAU_X, PLATEAU_X]
PLATEAU_X = 11.0
CLIFF_TOP = 3.9      # grass edge of the terrace
CLIFF_BASE = 2.8     # foot of the cliff face on the lower ground
STAIR_HALF = 1.7     # stairs span x in [-1.7, 1.7]

PLAZA = (0.0, -0.8)
PLAZA_R = 4.0
FOUNTAIN = (0.0, -0.6)

GATE = (0.0, 6.0)          # tower gate base
START_ZONE = (0.0, 4.95)

# buildings: name -> (base centre x, y)
SHOP = (-9.0, 1.0)
LIBRARY = (9.6, 1.4)
BARBER = (9.4, -5.4)
HOUSE_A = (-13.0, -3.6)
HOUSE_B = (-6.8, -7.4)
TAVERN = (13.8, -1.6)
SMITHY = (-14.6, 3.6)
HOUSE_C = (14.6, 5.6)

POND = (-13.6, -8.2)
POND_R = (3.0, 1.35)

# dirt roads (polylines, width in units)
ROADS = [
    ([(0.0, CLIFF_BASE - 0.1), (0.0, -0.8)], 2.4),                 # stairs -> plaza
    ([(0.0, -0.8), (0.0, -10.5)], 2.2),                            # plaza -> south exit
    ([(-3.6, -0.6), (-6.5, 0.1), (-9.0, 0.0)], 1.7),               # plaza -> shop
    ([(-9.0, 0.0), (-12.2, 1.4), (-14.6, 2.6)], 1.3),              # shop -> smithy
    ([(3.6, -0.6), (6.8, 0.2), (9.6, 0.3)], 1.7),                  # plaza -> library
    ([(9.6, 0.3), (12.4, -1.0), (13.8, -2.6)], 1.3),               # library -> tavern
    ([(2.8, -3.6), (6.0, -5.6), (9.4, -6.4)], 1.5),                # plaza -> barber
    ([(-2.8, -3.6), (-6.8, -6.4), (-6.8, -8.2)], 1.4),             # plaza -> house B
    ([(-6.8, -6.4), (-10.6, -5.2), (-13.0, -4.6)], 1.2),           # -> house A
    ([(12.4, -1.0), (14.6, 1.6), (14.6, 4.6)], 1.1),               # -> house C
]

# terrace path: stairs top -> gate platform
TERRACE_PATH = ([(0.0, CLIFF_TOP), (0.0, GATE[1] - 0.6)], 2.6)


def px(x, y):
    return (x + HALF_W) * 100.0, (HALF_H - y) * 100.0
