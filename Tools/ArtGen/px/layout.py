# World-unit layout (x right, y up). Map is 36x20 units, PPU 32. Mirror these in TowerBootstrap.BuildVillage.
CLIFF_TOP=3.9; CLIFF_BASE=2.8; PLATEAU_X=11.0; STAIR_HALF=1.75
GATE=(0.0,6.0); FOUNTAIN=(0.0,-0.6); PLAZA_R=3.6
PLAZA_CY=-1.15; PLAZA_RX=4.8; PLAZA_RY=3.0; FOUNTAIN_BASIN_Y=-1.4
ROAD_Y=-0.6; LANE_Y=-8.7
# name: (file, base x, base y, front width(units))
BUILDINGS=dict(
 Shop=('Bld_Shop',-8.6,0.9), Library=('Bld_Library',9.6,1.4), Barber=('Bld_Barber',8.8,-7.2),
 Tavern=('Bld_Tavern',14.6,0.6), Smithy=('Bld_Smithy',-15.0,3.4),
 HouseA=('Bld_HouseA',-13.4,-5.0), HouseB=('Bld_HouseB',-7.2,-7.4), HouseC=('Bld_HouseC',15.0,6.6),
)
WINDMILL=(-7.6,5.2)
FIELD=(-10.7,6.7,-4.3,9.7)   # x0,y0,x1,y1 tilled soil on west terrace
GARDEN=(4.4,7.2,10.4,9.7)    # fenced veggie patch on east terrace
POND=(-14.2,-8.1)
CAMPFIRE=(7.4,5.6)
