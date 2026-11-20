# Sage in Docker

Four containers: **Admin API**, **User API**, **Admin Panel**, **User Panel**. The database stays on the existing SQL
Server (a database container can be added later). Each panel is nginx serving the Angular app and forwarding `/api`
to its API, so the APIs are not exposed and need no CORS.

| Container | Image | Port on the host |
|---|---|---|
| user-panel | `ghcr.io/krishna16101982/sage-user-panel` | 9081 |
| admin-panel | `ghcr.io/krishna16101982/sage-admin-panel` | 9082 |
| user-api | `ghcr.io/krishna16101982/sage-user-api` | (internal 8080) |
| admin-api | `ghcr.io/krishna16101982/sage-admin-api` | (internal 8080) |

Logs and order-form PDFs are kept in Docker volumes (`admin-logs`, `user-logs`, `order-forms`).

## CI/CD (GitHub Actions, `.github/workflows/ci.yml`)

* Every push / pull request: API tests (SQL Server in a CI container), both panels built.
* Push to `main` (or *Run workflow* by hand): the 4 images are built and pushed to `ghcr.io` with tags `latest` and
  the commit SHA.
* Deploying = on the Docker machine: `docker compose pull && docker compose up -d`.

The images are private by default. On the Docker machine, log in once with a GitHub token that has `read:packages`:

```bash
docker login ghcr.io -u <github-user>
```

## Settings: `.env`

Copy `.env.example` to `.env` next to `docker-compose.yml` and fill it in (never commit it):

* `DB_CONNECTION`: the existing SQL Server. Inside a container `localhost` is the container itself, so use the
  server's IP and a fixed TCP port, e.g. `Server=10.10.61.100,1433;Database=SageAdmin_Dev;User Id=sage_user;Password=...;TrustServerCertificate=true`.
  SQL Express uses a random port by default: in *SQL Server Configuration Manager > SQL Server Network Configuration >
  Protocols for SQLEXPRESS > TCP/IP > IP Addresses > IPAll* clear *TCP Dynamic Ports*, set *TCP Port* = 1433, restart
  the SQL Server service, and allow TCP 1433 in the Windows firewall.
* `ADMIN_JWT_KEY`, `USER_JWT_KEY`: two different random secrets (32+ characters).
* `ADMIN_PANEL_URL`, `USER_PANEL_URL`: the addresses users open (password-reset email links).
* `COOKIE_SECURE=false` while on plain http.

## Run

```bash
docker compose pull      # images from GitHub (after CI has run on main)
docker compose up -d
docker compose ps
docker compose logs -f admin-api
```

or build on the machine itself: `docker compose up -d --build`.

Open `http://<machine>:9082` (Admin Panel) and `http://<machine>:9081` (User Panel).
Update after a new CI build: `docker compose pull && docker compose up -d`.

## Docker on the Windows server (10.10.61.100)

The images are Linux images, so Windows Server runs them through **WSL2** (Docker Desktop is not licensed for
Windows Server). The server is a virtual machine, so its host must allow **nested virtualization**.

1. Admin PowerShell: `wsl --install -d Ubuntu-24.04`, reboot, create the Ubuntu user.
2. In Ubuntu: `curl -fsSL https://get.docker.com | sudo sh`, then `sudo usermod -aG docker $USER` and log in again.
3. Copy `docker-compose.yml` and your `.env` into a folder in Ubuntu (e.g. `~/sage`), `docker login ghcr.io`,
   then `docker compose pull && docker compose up -d`.
4. Reach the containers from other machines. Either use mirrored networking (Windows Server 2025 / Windows 11):
   create `C:\Users\<you>\.wslconfig` with
   ```
   [wsl2]
   networkingMode=mirrored
   ```
   then `wsl --shutdown` and start Ubuntu again; or forward the ports with `netsh interface portproxy` to the WSL IP.
   Allow TCP 9081-9082 in the Windows firewall (and in the network NAT for the public IP, as for 8081-8084).

IIS keeps running on 8081-8084, so the IIS and Docker versions can run side by side until you switch.
