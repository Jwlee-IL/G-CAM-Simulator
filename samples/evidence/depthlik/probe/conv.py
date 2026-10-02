import csv,sys,statistics as st,collections
def load(f):
    r=list(csv.reader(open(f))); h=r[0]; out={}
    for x in r[1:]:
        if x[0]!='FIT' or x[1]=='tag': continue
        d=dict(zip(h,x)); out[(float(d['z']),d['channel'],d['seed'])]=d
    return out
files=sys.argv[1:]; D={f:load(f) for f in files}; ref=files[-1]
print("reference:",ref)
for f in files:
    for z in (500.0,1000.0):
        for ch in ('all','win'):
            keys=[k for k in D[ref] if k[0]==z and k[1]==ch and k in D[f] and all(float(D[g][k]['lambda_truth'])>-10 for g in files if k in D[g])]
            excl=[k[2] for k in D[ref] if k[0]==z and k[1]==ch and k not in keys]
            if len(keys)<2: continue
            est=[float(D[f][k]['zhat']) for k in keys]; rf=[float(D[ref][k]['zhat']) for k in keys]
            dif=[a-b for a,b in zip(est,rf)]; lam=[float(D[f][k]['lambda_truth']) for k in keys]
            print(f"{f:28} z={z:5.0f} {ch} n={len(keys)} mean_bias={st.mean(est)-z:+7.2f} seed_sd={st.stdev(est):6.2f} diff_vs_ref mean={st.mean(dif):+6.2f} sd={st.stdev(dif) if len(dif)>1 else 0:5.2f} max|d|={max(abs(x) for x in dif):5.2f} lam_mean={st.mean(lam):.2f} excluded(search failure in any budget)={excl}")
