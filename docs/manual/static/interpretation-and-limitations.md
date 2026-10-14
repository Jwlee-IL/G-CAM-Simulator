> DRAFT — static chapter for the author's review (HUM-12); sourced from the repository documents cited in each paragraph. Remove this line when approved.

[[notice:N-SIM]]

#### Images at the default optics

With the default optics and the 1 m focal plane the detector samples the mask-cell shadow at 1.27 pixels per cell
(0.761 mm shadow on 0.6 mm pixels), below two samples per cell. The decoded peak then shifts with the source position:
over y = −10 … 10 mm the error is 0.95 mm RMS and 1.91 mm at most. Finer pixels reduce it (0.24 mm RMS at 3.8 samples per
cell); the default optics reach two samples per cell only near a 160 mm focal plane. Read a peak position at the default
optics with that spread in mind. Sources: `docs/VV.Studio.Imaging.md` (Localisation bias); `docs/VV.Studio.md` AN-11.

#### What the locks and measurements guarantee

- The images always belong to the inputs shown: physical inputs are locked while data exist, and Reset discards the data
  before an edit. Source: `docs/VV.Studio.md` §6 (risk table; AN-06).
- Positions read off an image use one screen-to-millimetre mapping with the decoder's pixel-centre convention. An ROI
  counts whole pixels by their centres; values are shown at 0.1 resolution, not to a sub-pixel precision. Source:
  `docs/VV.Studio.md` §6 (risk table).

#### Isotope stripping

With the Compton strip view the displayed flood is clamped at zero, while cross-correlation decodes the signed
difference; both reconstruction methods report the signed net count as N ± σ (one standard deviation, counting and
calibration). A negative net is shown as it is and implies no detection verdict, and the scalar model removes no
ambient background. Stripping removes the higher isotope's downscatter in the mean, not its noise: the smallest
detectable Cs-137 source under 10 µSv/h of Co-60 at 1 m is 1.6 MBq in 60 s. Sources: `docs/VV.Studio.SRS.md` SR-IMG-05;
`docs/VV.Studio.Imaging.md` (Strip count and signed decoding); `README.md` (headline results).

#### Experimental physical readout

[[notice:N-READOUT]]

#### Known gaps of the viewer

- Measurements can only be created with the pointer; keyboard users can review, select and delete them. Source:
  `docs/VV.Studio.md` AN-01.
- Windows high-contrast themes are not honoured. Source: `docs/VV.Studio.md` AN-03.
- No screen-reader session has been run, and keyboard-only flows and other display scaling remain unverified. Source:
  `docs/VV.Studio.md` §6 and AN-02.
- End-to-end responsiveness during a window resize is not established. Source: `docs/VV.Studio.md` AN-13.
- On one machine a render test found a truncated table value in the Spectrum line table; the cause is not established.
  Source: `docs/VV.Studio.md` AN-15.
