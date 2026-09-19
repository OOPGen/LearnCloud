# Brand assets

`learncloud-logo.jpeg` is the official LearnCloud logo, as supplied. Everything else is cut
from it, so change the logo here and regenerate rather than editing the copies.

The artwork is on a white background (no transparency), so on dark surfaces — the app
sidebar, the marketing footer — the mark sits on a white tile rather than being keyed out:
the graduation cap is dark navy and would disappear against a dark background.

## Generated files

| File | Size | Where it is used |
|---|---|---|
| `logo.png` | 720 px wide | Sign-in and email-link pages, app home header, marketing header |
| `logo-mark.png` | 512×512 | App sidebar and mobile drawer, marketing footer |
| `favicon.png` | 64×64 | Browser tab (both sites) |
| `apple-touch-icon.png` | 180×180 | Home screen icon (both sites) |
| `og-image.png` | 1200×630 | Link previews for the marketing site |

They are written into `src/LearnCloud.Web/public/` and `marketing-site/public/`, which both
Workers serve as static files.

## Regenerating

Needs `sharp` (not a project dependency; install it anywhere and run the script with node):

```bash
npm install sharp
node brand/build-assets.mjs brand/learncloud-logo.jpeg src/LearnCloud.Web/public marketing-site/public
```

The crop boxes in the script were measured from the artwork's ink profile: the mark ends at
y=570, where the "ERP" badge begins. A new logo with a different layout needs those numbers
re-measured, not guessed.
