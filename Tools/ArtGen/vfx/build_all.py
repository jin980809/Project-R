import importlib, shutil, os
from core import SK, write_skill
for m in ('job_sword_spear','job_bow_staff','job_axe_blunt','job_rest'): importlib.import_module(m)
shutil.rmtree('out', ignore_errors=True); os.makedirs('out')
for n in SK: write_skill(n, 'out')
print(len(SK))
