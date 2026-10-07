# Prototype font

RubbishPrototypeSans.ttf is a modified static, subsetted derivative of Noto Sans SC Regular.

- Upstream: https://github.com/google/fonts/tree/main/ofl/notosanssc
- Font input: NotoSansSC[wght].ttf, downloaded 2026-10-06 from the Google Fonts repository.
- License: SIL Open Font License 1.1, reproduced in OFL.txt.
- Derivative family name: Rubbish Prototype Sans; no reserved Source name is used.
- Changes: instantiate weight 400; subset to ASCII and characters used by the project; rename the derivative family.
- Reproduce: Tools/PreparePrototypeFont.py (fontTools); downloaded upstream input remains in ignored Artifacts/Fonts.

Keep OFL.txt when redistributing the font. The Windows build script copies the license next to the executable.
