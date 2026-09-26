# ElenskiBalkandzii.Server

ASP.NET Core Web API backend for Elenski Balkandzii using PostgreSQL.

Set these environment variables on the API service before deployment:

- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string.
- `Admin__Password`: private admin password (never put it in the client build).
- `Admin__SessionSecret`: independent random secret of at least 32 UTF-8 bytes, e.g. a 64-character hex string.
- `Cors__AllowedOrigins__0`: `https://elenskibalkandzii-client.onrender.com` (or the actual frontend origin).

The service creates its `site_content` table on startup. API: `GET /api/content` (public), `POST /api/admin/login` (rate limited), `GET /api/admin/session`, `PUT /api/admin/content` (Bearer token, expires in 8 hours). There are no customer accounts. Deploy the API before setting `NEXT_PUBLIC_API_URL` on the static client and rebuilding it.

The service also creates `site_images` in PostgreSQL. `POST /api/images` accepts an authenticated JPEG/PNG/WebP upload of up to 5 MB; `GET /api/images/{id}` serves it publicly.

Catalog content consists of ordered, bilingual categories and products with descriptions, optional images, and visibility flags. Category and product IDs must match in both languages. No prices, cart, checkout or customer accounts are part of the API.
