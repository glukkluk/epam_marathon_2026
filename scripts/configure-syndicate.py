"""Generate account-specific Syndicate configuration on the CI runner."""

import os
from pathlib import Path

import yaml


def main():
    account = os.environ["AWS_ACCOUNT_ID"]
    if len(account) != 12 or not account.isdigit():
        raise ValueError("AWS_ACCOUNT_ID must contain 12 digits")
    region = os.environ["AWS_REGION"]
    app = Path(os.environ["APP_DIR"]).resolve()
    config_dir = app / ".syndicate-config-restaurant-backend-app"
    updates = {
        "syndicate.yml": {
            "project_path": str(app),
            "deploy_target_bucket": f"marathon2026-restaurant-artifacts-{account}",
        },
        "syndicate_aliases.yml": {
            "imagesBucketName": f"restaurant-backend-dev-images-{account}",
            "profileImagesBucketName": f"restaurant-backend-dev-profile-images-{account}",
            "swagger_ui_hoster_bucket_name": f"restaurant-backend-dev-swagger-ui-{account}",
            "email_from": os.environ.get("EMAIL_FROM", ""),
        },
    }
    for filename, values in updates.items():
        path = config_dir / filename
        config = yaml.safe_load(path.read_text())
        config.update(account_id=account, region=region, **values)
        path.write_text(yaml.safe_dump(config, sort_keys=False))


if __name__ == "__main__":
    main()
