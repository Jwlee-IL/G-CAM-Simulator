"""Generates the interaction tables in src/Gcam.Detector/CrystalMaterial.cs.

Requires xraylib (tested with 4.3.0: `pip install xraylib`). Writes crystal_tables.json; the C# arrays are that
JSON's E / mu / pf columns. Method (also in the CrystalMaterial remarks): photo + incoherent cross sections from
xraylib to 800 keV with points straddling every K edge; above 800 keV Klein-Nishina x electrons/g plus a log-log
extrapolated photoelectric term; coherent scattering and pair production deliberately excluded.
"""
import xraylib as x, math, json, re
RE=2.8179403262e-13; NA=6.02214076e23; ME=510.99895
def kn(E):  # Klein-Nishina total cross section per electron, cm2
    k=E/ME; a=1+k
    return 2*math.pi*RE**2*( a/k**2*(2*a/(1+2*k)-math.log(1+2*k)/k) + math.log(1+2*k)/(2*k) - (1+3*k)/(1+2*k)**2 )
def e_per_g(formula):
    cp=x.CompoundParser(formula); s=0
    for z,w in zip(cp['Elements'],cp['massFractions']): s+=w*z/x.AtomicWeight(z)
    return s*NA
def photo(formula,E):
    if E<=800: return x.CS_Photo_CP(formula,E)
    p6,p8=x.CS_Photo_CP(formula,600),x.CS_Photo_CP(formula,800)
    return p8*(E/800)**(math.log(p8/p6)/math.log(800/600))
def compt(formula,E):
    return x.CS_Compt_CP(formula,E) if E<=800 else kn(E)*e_per_g(formula)
mats=[("GAGG","Gd3Al2Ga3O12",6.63,"GAGG:Ce"),("GAGG_Mg","Gd3Al2Ga3O12",6.63,"GAGG:Ce,Mg"),
      ("CeBr3","CeBr3",5.10,"CeBr3"),("LaBr3","LaBr3",5.08,"LaBr3:Ce"),("LYSO","Lu1.8Y0.2SiO5",7.10,"LYSO:Ce"),
      ("BGO","Bi4Ge3O12",7.13,"BGO"),("NaI","NaI",3.67,"NaI:Tl")]
base=[20,30,40,50,60,80,100,150,200,300,400,500,600,661.7,800,1000,1250,1500,2000,3000]
out=[]
for key,f,rho,label in mats:
    cp=x.CompoundParser(f); grid=set(base)
    for z in cp['Elements']:
        ek=x.EdgeEnergy(z,x.K_SHELL)
        if 20<ek<3000: grid.add(round(ek*(1-1e-4),4)); grid.add(round(ek*(1+1e-4),4))
    E=sorted(grid); mu=[photo(f,e)+compt(f,e) for e in E]; pf=[photo(f,e)/(photo(f,e)+compt(f,e)) for e in E]
    out.append(dict(key=key,label=label,formula=f,rho=rho,E=E,mu=mu,pf=pf))
json.dump(out, open('crystal_tables.json', 'w'), indent=1)
for m in out:
    i = m['E'].index(661.7)
    print(f"{m['key']:8s} mu662 = {m['mu'][i] * m['rho'] / 10:.4f} /mm, photo fraction {m['pf'][i]:.3f}")
