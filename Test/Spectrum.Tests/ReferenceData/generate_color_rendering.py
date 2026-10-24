"""Regenerate tables/independent fixtures with colour-science==0.4.7 (development only).

Run from the repository root. Runtime code has no Python dependency.
"""
from pathlib import Path
import csv
import json
import numpy as np
import colour
from colour.quality import cri
from colour.temperature.robertson1968 import ISOTEMPERATURE_LINES_ROBERTSON1968

ROOT = Path(__file__).resolve().parents[3]
DATA = ROOT / "Engine/cvColorVision/Devices/Spectrometer/ColorRenderingData"
DATA.mkdir(parents=True, exist_ok=True)
shape = colour.SpectralShape(380, 780, 1)
wl = shape.wavelengths
cmfs = colour.MSDS_CMFS["CIE 1931 2 Degree Standard Observer"].copy().align(shape)
samples = dict(cri.SDS_TCS["CIE 1995"])
# Optional East Asian skin sample from JIS Z 8726; it is not included in Ra.
samples["TCS15"] = cri.SDS_TCS["CIE 2024"]["TCS15"]
tcs = {name: colour.SpectralDistribution(dict(zip(wl, np.interp(wl, sd.wavelengths, sd.values))))
       for name, sd in samples.items()}
daylight = colour.colorimetry.SDS_BASIS_FUNCTIONS_CIE_ILLUMINANT_D_SERIES
with (DATA / "cie-cri-tables.csv").open("w", newline="", encoding="utf-8") as f:
    writer = csv.writer(f, lineterminator="\n")
    writer.writerow(["nm", "x", "y", "z", "S0", "S1", "S2", *tcs])
    for i, wavelength in enumerate(wl):
        writer.writerow([int(wavelength), *cmfs.values[i],
                         *[np.interp(wavelength, daylight[k].wavelengths, daylight[k].values) for k in ("S0", "S1", "S2")],
                         *[sd.values[i] for sd in tcs.values()]])
with (DATA / "robertson-1968.csv").open("w", newline="", encoding="utf-8") as f:
    writer = csv.writer(f, lineterminator="\n")
    writer.writerow(["mired", "u", "v", "slope"])
    writer.writerows([[line.r, line.u, line.v, line.t] for line in ISOTEMPERATURE_LINES_ROBERTSON1968])

fixtures = []
sources = {name: colour.SDS_ILLUMINANTS[name].copy().align(shape) for name in ("D65", "FL2", "FL4", "LED-B1")}
sources["Planck2856"] = colour.sd_blackbody(2856, shape)
sources["Planck4500"] = colour.sd_blackbody(4500, shape)
sources["Daylight5001"] = colour.sd_CIE_illuminant_D_series(colour.temperature.CCT_to_xy_CIE_D(5001)).align(shape)
for name, sd in sources.items():
    # Explicit integration and linear TCS interpolation follow CIE 2019 PO044.
    xyz = colour.sd_to_XYZ(sd, cmfs, method="Integration")
    uv = colour.UCS_to_uv(colour.XYZ_to_UCS(xyz))
    cct, duv = colour.temperature.uv_to_CCT_Robertson1968(uv)
    reference = (colour.sd_blackbody(cct, shape) if cct < 5000 else
                 colour.sd_CIE_illuminant_D_series(colour.temperature.CCT_to_xy_CIE_D(cct)).align(shape))
    test = cri.tcs_colorimetry_data(sd, reference, tcs, cmfs, chromatic_adaptation=True, method="CIE 2024")
    ref = cri.tcs_colorimetry_data(reference, reference, tcs, cmfs, method="CIE 2024")
    ri = [100 - 4.6 * float(np.linalg.norm(a.UVW - b.UVW)) for a, b in zip(test, ref)]
    spd = sd.values / max(sd.values)
    fixtures.append(dict(name=name, spectrum=spd.tolist(), xy=colour.XYZ_to_xy(xyz).tolist(), cct=float(cct), duv=float(duv),
                         ra=float(np.mean(ri[:8])), ri=ri,
                         mean=float(np.trapezoid(wl * spd, wl) / np.trapezoid(spd, wl))))
(Path(__file__).parent / "color-rendering.json").write_text(json.dumps(fixtures, indent=2) + "\n", encoding="utf-8")
print(f"Generated {len(wl)} table rows and {len(fixtures)} reference spectra with Colour {colour.__version__}.")

chromaticities = []
for x,y in [(0.54369557,0.32107944), (0.37605506,0.24452225), (0.3,0.6), (0.25,0.3),
            (0.33,0.4), (0.45,0.3), (0.2,0.1), (0.32,0.31), (0.42,0.22), (0.15,0.25)]:
    wavelength, boundary, opposite = colour.dominant_wavelength([x,y], [0.3127,0.3290], cmfs)
    chromaticities.append(dict(x=x, y=y, wavelength=float(wavelength),
        purity=float(colour.excitation_purity([x,y], [0.3127,0.3290], cmfs))))
(Path(__file__).parent / "chromaticity.json").write_text(json.dumps(chromaticities, indent=2)+"\n", encoding="utf-8")

# Independent locus and white-mixture boundary coordinates, including the violet endpoint.
boundaries = []
for wavelength in (380, 400, 435, 546, 615, 650):
    xyz = colour.wavelength_to_XYZ(wavelength, cmfs)
    xy = colour.XYZ_to_xy(xyz)
    for purity in (1.0, 0.5):
        sample = np.array([0.3127, 0.3290]) + purity * (xy - np.array([0.3127, 0.3290]))
        boundaries.append(dict(x=float(sample[0]), y=float(sample[1]), wavelength=wavelength, purity=purity))
(Path(__file__).parent / "chromaticity.json").write_text(json.dumps(chromaticities + boundaries, indent=2)+"\n", encoding="utf-8")

# Independent display reference: same E -> D65 CAT02 and whole-domain normalization
# used by Colour plot_single_sd, with the display domain fixed at 380-780 nm.
from colour.plotting import XYZ_to_plotting_colourspace
linear_rgb = XYZ_to_plotting_colourspace(
    colour.wavelength_to_XYZ(wl),
    colour.CCS_ILLUMINANTS["CIE 1931 2 Degree Standard Observer"]["E"],
    apply_cctf_encoding=False,
)
display_rgb = np.rint(255 * colour.models.eotf_inverse_sRGB(
    colour.algebra.normalise_maximum(linear_rgb))).astype(int)
(ROOT / "Test/Spectrum.Tests/ReferenceData/spectrum-display-rgb.json").write_text(
    json.dumps(np.column_stack((wl.astype(int), display_rgb)).tolist(), separators=(",", ":")) + "\n",
    encoding="utf-8",
)
