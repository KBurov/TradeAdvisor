# Setup — Windows 11 + WSL2 (Ubuntu) + RTX

This guide records the exact steps to prepare a Windows laptop (with RTX GPU) for the TradeAdvisor project.

---

## 1) Enable WSL2 (one time, in Windows PowerShell as Admin)

```powershell
wsl --install
wsl --set-default-version 2
```

Install **Ubuntu** from Microsoft Store. Launch it and create your UNIX user.

---

## 2) Update Ubuntu (inside the Ubuntu terminal)

```bash
sudo apt-get update
sudo apt-get upgrade -y
```

---

## 3) Essentials (inside Ubuntu)

```bash
sudo apt-get install -y git build-essential make curl wget unzip jq ca-certificates pkg-config libpq-dev postgresql-client
```

---

## 4) Python 3.11 (inside Ubuntu)

```bash
sudo apt-get install -y python3 python3-venv python3-pip
python3 --version
pip3 --version
```

---

## 5) .NET 8 SDK (LTS) (inside Ubuntu)

```bash
wget https://packages.microsoft.com/config/ubuntu/$(. /etc/os-release; echo $VERSION_ID)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --info
dotnet --list-sdks
```

> We use **.NET 8 (LTS)** for now. We can upgrade to .NET 9 later when the official packages are broadly available for our distro.

---

## 6) Docker Desktop (Windows)
- Install **Docker Desktop** on Windows.
- Open Docker Desktop → Settings → General: ensure **Use the WSL 2 based engine** is enabled (default).
- No need to enable Windows containers.

**Verify from Ubuntu:**

```bash
docker version
```

**Test GPU pass-through (still in Ubuntu):**

```bash
docker run --rm --gpus all nvidia/cuda:12.4.1-base-ubuntu22.04 nvidia-smi
```

You should see `NVIDIA-SMI` with your **GeForce RTX 2080**.

---

## 7) VS Code (Windows) + WSL Extension
- Install **VS Code** for Windows.
- Install the **Remote – WSL** extension.
- From the project folder in Ubuntu: code .

---

## 8) Git (inside Ubuntu) — identity + SSH (recommended)

```bash
git config --global user.name  "Your Name"
git config --global user.email "your-email@example.com"
git config --global init.defaultBranch main
git config --global core.autocrlf input
git config --global color.ui auto
```

### Generate SSH key and add to GitHub

```bash
ssh-keygen -t ed25519 -C "your-email@example.com"
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/id_ed25519
cat ~/.ssh/id_ed25519.pub
```

Copy the printed key → GitHub → Settings → SSH and GPG keys → New SSH key → paste → save.

**Test:**

```bash
ssh -T git@github.com
```

You should see a success message.

---

## 9) Create repo (CLI) and clone (example)

```bash
mkdir -p ~/Projects && cd ~/Projects
gh config set git_protocol ssh
gh repo create TradeAdvisor --private --clone
cd TradeAdvisor
```

---

## 10) Quality-of-life (optional)
- Docker Desktop → Resources: allocate 8–16 GB RAM if you plan to run many containers.
- Keep the repo under **Linux path** (e.g., `/home/<you>/Projects`) for best performance (avoid `/mnt/c/...`).

---

## Troubleshooting
- `docker version` shows only Client → Start Docker Desktop on Windows.
- GPU test fails → update NVIDIA driver on Windows; ensure Docker Desktop is latest.
- `ssh -T git@github.com` fails → re-add key to GitHub, or check you used the correct email during `ssh-keygen`.
