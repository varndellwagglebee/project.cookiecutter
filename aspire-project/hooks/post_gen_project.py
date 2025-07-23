#!/usr/bin/env python
"""
This hook runs automatically after Cookiecutter finishes generating the project.
It captures the exact Git commit SHA of the template you used, then writes it
into the generated project's .cookiecutter.json under "template_sha" so future
template updates can update the project correctly.

 ***Aborts generation*** when the template is not a Git repo (no .git).

Works both:
• Interactively on dev machines (Windows/macOS/Linux)
• Non-interactive in CI runners (e.g., GitHub Actions ubuntu-latest)
"""
from __future__ import annotations
import json, shutil, subprocess, sys
from pathlib import Path
from typing import Iterable

# ──────────────────────────────────────────── #
# Paths
# ──────────────────────────────────────────── #
ROOT = Path.cwd()
SRC  = ROOT / "src" / "{{ cookiecutter.assembly_name }}"

# ──────────────────────────────────────────── #
# Helpers
# ──────────────────────────────────────────── #
def rm(item: Path | str) -> None:
    p = Path(item)
    if p.exists():
        shutil.rmtree(p) if p.is_dir() else p.unlink()
        print(f"🗑️  Removed {'dir' if p.is_dir() else 'file'}: {p}")

def rm_each(paths: Iterable[Path | str]) -> None:
    for p in paths: rm(p)

_yes = lambda s: (s or "").strip().lower() == "yes"

def find_git_root(start: Path) -> Path | None:
    current = start
    while True:
        if (current / ".git").exists():
            return current
        if current.parent == current:
            return None       # reached filesystem root
        current = current.parent

def read_sha(repo: Path) -> str | None:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repo), "rev-parse", "HEAD"],
            stderr=subprocess.DEVNULL,
        ).decode().strip()
    except Exception:
        return None

# ──────────────────────────────────────────── #
# Answers
# ──────────────────────────────────────────── #
include_azure_key_vault = _yes("{{ cookiecutter.include_azure_key_vault }}")
include_azure_application_insights = _yes("{{ cookiecutter.include_azure_application_insights }}")  
include_azure_storage = _yes("{{ cookiecutter.include_azure_storage }}")
include_azure_service_bus = _yes("{{ cookiecutter.include_azure_service_bus }}")
database_is_pg = "{{ cookiecutter.database }}" == "PostgreSql"
include_audit = _yes("{{ cookiecutter.include_audit }}")
include_oauth = _yes("{{ cookiecutter.include_oauth }}")
aspire_deploy = _yes("{{ cookiecutter.aspire_deploy }}")
github_deployment = _yes("{{ cookiecutter.github_deployment }}")
project_path = "{{ cookiecutter.project_path }}"
template_path = "{{ cookiecutter.template_path }}"
deployment_environment = "{{ cookiecutter.deployment_environment }}"

# ──────────────────────────────────────────── #
# 1️⃣ Conditional cleanup
# ──────────────────────────────────────────── #

# Database
if not database_is_pg:
    rm_each([
        ROOT / "src/{{ cookiecutter.assembly_name }}.Migrations/Resources/samples",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Configuration/DatabaseConfigurationPostgres.cs",
    ])
else:
    rm_each([
        ROOT / "src/{{ cookiecutter.assembly_name }}.Migrations/Resources/1000-Initial/CreateSample.sql",
    ])

# Azure
if not include_azure_key_vault:
    rm_each([
        SRC / ".Core/Vault",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Extensions/AzureSecretsExtensions.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Configuration/AzureKeyVaultConfiguration.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Migrations/appsettings.Production.json",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Migrations/appsettings.Staging.json",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Migrations/Extensions/AzureSecretsExtensions.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Identity/CryptoRandom.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Vault"
    ])

if not include_azure_application_insights:
    rm_each([
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Extensions/ApplicationInsightsExtensions.cs",
    ])

if not include_azure_service_bus:
    rm_each([
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Commands/Middleware/CommandMiddlewareTelemetryExtensions.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Commands/Middleware/TelemetrySourceProvider.cs"
    ])

if not include_audit:
    rm_each([
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Security/SecureAttribute.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Security/SecurityHelper.cs",
    ])

if not include_oauth:
    rm_each([
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Identity/AuthService.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Configuration/CryptoRandom.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Infrastructure/Extensions/SecurityRequirementsOperationFilter.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Core/Extensions/AuthPolicyExtensions.cs",
        ROOT / "src/{{ cookiecutter.assembly_name }}.Data/Abstractions/IAuthService.cs",
    ])

rm(ROOT / "templates")  # always drop template snippets

# ──────────────────────────────────────────── #
# 2️⃣ Optional deployment helper
# ──────────────────────────────────────────── #
if aspire_deploy and project_path:
    try:
        subprocess.run(
            [
                sys.executable,
                str(ROOT / "deployment.py"),
                deployment_environment,
                "{{ cookiecutter.assembly_name }}",
                str(github_deployment).lower(),
                "{{ cookiecutter.database }}",
                project_path,
                template_path,
            ],
            check=True,
        )
    except subprocess.CalledProcessError as exc:
        print(f"❌ Deployment helper failed: {exc}")
        sys.exit(exc.returncode)

# ──────────────────────────────────────────── #
# 2️⃣.5️⃣ Capture template SHA – robust
# ──────────────────────────────────────────── #
template_sha: str | None = None

# Check cookiecutter._template path first (for Git URL usage)
template_arg = Path("{{ cookiecutter._template }}").expanduser()
git_root = find_git_root(template_arg)
if git_root:
    template_sha = read_sha(git_root)

# Fallback to relative .git from this post_gen_script
if not template_sha:
    hook_path = Path(__file__).resolve()
    fallback_root = hook_path.parent.parent
    git_root = find_git_root(fallback_root)
    if git_root:
        template_sha = read_sha(git_root)

if not template_sha:
    print("❌ Template is not a git repository – cancelling project creation.")
    sys.exit(1)

print(f"✅ Template commit SHA: {template_sha}")

# ──────────────────────────────────────────── #
# 3️⃣ Persist minimal replay context
# ──────────────────────────────────────────── #
COOKIE_FILE = ROOT / ".cookiecutter.json"
if not COOKIE_FILE.exists():
    ctx: dict[str, str] = {
        "is_aspire": "yes",
        "assembly_name": "{{ cookiecutter.assembly_name }}",
        "root_namespace": "{{ cookiecutter.root_namespace }}",
        "api_app_name": "{{ cookiecutter.api_app_name }}",
        "api_web_url": "{{ cookiecutter.api_web_url }}",
        "database": "{{ cookiecutter.database }}",
        "database_name": "{{ cookiecutter.database_name }}",
        "aspire_deploy": "{{ cookiecutter.aspire_deploy }}",
        "github_deployment": "{{ cookiecutter.github_deployment }}",
        "deployment_environment": deployment_environment,
        "project_path": project_path,
        "template_path": template_path,
        "include_audit": "{{ cookiecutter.include_audit }}",
        "include_oauth": "{{ cookiecutter.include_oauth }}",
        "include_azure_key_vault": "{{ cookiecutter.include_azure_key_vault }}",
        "include_azure_application_insights": "{{ cookiecutter.include_azure_application_insights }}",
        "include_azure_storage": "{{ cookiecutter.include_azure_storage }}",
        "include_azure_service_bus": "{{ cookiecutter.include_azure_service_bus }}",
        
        "oauth_client_id": "{{ cookiecutter.oauth_client_id }}",
        "oauth_client_secret": "{{ cookiecutter.oauth_client_secret }}",
        "oauth_authority": "{{ cookiecutter.oauth_authority }}",
        "oauth_audience": "{{ cookiecutter.oauth_audience }}",
        
        "key_vault_name": "{{ cookiecutter.key_vault_name }}",
        "key_vault_tenant_id": "{{ cookiecutter.key_vault_tenant_id }}",
        "key_vault_client_id": "{{ cookiecutter.key_vault_client_id }}",
        "key_vault_secret_name": "{{ cookiecutter.key_vault_secret_name }}",
            
        "app_insights_connection_string": "{{ cookiecutter.app_insights_connection_string }}",
        
        "storage_account_name": "{{ cookiecutter.storage_account_name }}",
        "storage_container_name": "{{ cookiecutter.storage_container_name }}",
        
        "service_bus_namespace": "{{ cookiecutter.service_bus_namespace }}",
        "service_bus_topic": "{{ cookiecutter.service_bus_topic }}",
        "service_bus_subscription": "{{ cookiecutter.service_bus_subscription }}",
            
        "template_sha": template_sha,
    }


    ctx = {k: v for k, v in ctx.items() if v not in ("", None)}
    COOKIE_FILE.write_text(json.dumps({"cookiecutter": ctx}, indent=4))
    print("✅  .cookiecutter.json written (with cookiecutter key)")

print("🎉 Aspire post-gen hook completed successfully")
