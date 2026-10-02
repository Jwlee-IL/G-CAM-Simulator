NIST XCOM 3.1 reference files used only for independent CsI checks.

Download page: https://physics.nist.gov/PhysRefData/Xcom/Text/download.html
Archive retrieved by GET: https://physics.nist.gov/PhysRefData/Xcom/XCOM.tar.gz?download=1
Archive SHA256: b2bdd0608bbece4809a337b7264c85b7997f608a1f8e2181ce5201421efcc0de

The four files were copied byte-for-byte from these archive entries:
XCOM/MDATX3.053 (iodine), XCOM/MDATX3.055 (cesium), XCOM/ATWTS.DAT,
XCOM/XCOM.f. Mac resource forks were not used. No Fortran program was built,
installed or executed. File hashes are recorded in ../CsI_checks.txt.

The source documents the data layout: atomic number/weight, edge count and
energy count, edge indices/labels/energies, then energy and five component
arrays (SCATCO, SCATIN, PHOT, PAIRAT, PAIREL). Array cross sections are in
barns/atom, energies in eV. Only exact 1000/1250-keV nodes are used.
XCOM.f's AVOG=0.60221367 divided by ATWTS.DAT's elemental weight converts
barns/atom to cm2/g. Components are mixed using NIST CsI weight fractions
I=0.488451 and Cs=0.511549. Raw components retain their published four
significant digits. Their precision is not used to hide extension errors.

NIST cautions that this downloadable version may differ from its online
version. It is a component reference for the omitted-scattering/pair budget,
not a replacement for the existing xraylib generator. The retained-component
differences at 1000/1250 keV are explicitly reported as approximation errors,
separate from extension-formula and below-total assertions. See the
download page for NIST's copyright and data disclaimer.
