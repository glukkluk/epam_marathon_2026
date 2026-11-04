# Деплой через GitHub Actions без IAM Identity Providers

Регіон за замовчуванням — **eu-north-1 (Stockholm)**. Усі три workflows
використовують access keys, а не OIDC: Identity Provider, `AWS_ROLE_ARN` та
`id-token: write` не потрібні. Account ID визначається через STS автоматично.

Відсутність доступу до IAM Identity Providers сама по собі не заважає цьому
способу. Але access keys не обходять обмежень акаунта: потрібні дозволи створювати
IAM roles/policies для Lambda та Scheduler і ресурси застосунку. Якщо це навчальний
акаунт із забороною таких операцій, адміністратор має надати дозволи або підготувати
ресурси й адаптувати конфігурацію до дозволених ролей. Free Tier не означає
автоматично ні заборони IAM, ні безкоштовності всіх ресурсів.

## 1. AWS Console

### Credentials

1. **IAM → Users → Create user**: наприклад, `github-restaurant-deploy`.
   Console access для CI не потрібен. Не створюй access keys root-користувача.
2. Надай deployment permissions. У репозиторії є початкова policy
   [`aws-deploy-policy.json`](aws-deploy-policy.json): **IAM → Policies → Create
   policy → JSON**, встав JSON і прикріпи policy до користувача.
   Вона надає широкі права на сервіси цього проєкту; призначена для окремого
   навчального акаунта, а не як мінімальна production policy.
3. **User → Security credentials → Create access key → Application running
   outside AWS**. Збережи Access key ID і Secret access key у GitHub Secrets.
   Якщо тобі видали тимчасові credentials лабораторного акаунта, використовуй
   усі три значення, включно з Session token, та оновлюй їх після завершення сесії.

### Terraform state bucket

**S3 → Create bucket**:

- Name: `restaurant-terraform-state-<твій 12-значний AWS account ID>`.
- Region: `eu-north-1` (або твій єдиний обраний регіон).
- Block all public access: **увімкнено**.
- Bucket Versioning: **Enable**.
- Encryption: **SSE-S3**.

Цей bucket треба створити до Terraform. DynamoDB-таблиця для lock не потрібна:
використовується S3 lockfile. Artifacts bucket для Syndicate створить backend workflow.

### S3 account-level Block Public Access

Поточний Terraform створює публічні read policies для **images** і **swagger-ui**.
Тому в **S3 → Block Public Access settings for this account** параметри
`BlockPublicPolicy` та `RestrictPublicBuckets` мають дозволяти ці policies.
`BlockPublicAcls` і `IgnorePublicAcls` можна залишити увімкненими.
State, artifacts, frontend і profile-images залишаються приватними на рівні bucket.
Якщо організація забороняє public policies, поточну схему images/swagger потрібно
переробити на приватний доступ через CloudFront перед запуском Terraform.

### Пошта та сповіщення

- Для надсилання звітів: **Amazon SES → Verified identities → Create identity →
  Email address** у тому самому регіоні. Підтверди лист і задай `EMAIL_FROM` у GitHub.
  У SES sandbox потрібно також верифікувати адреси одержувачів.
- Після Terraform підтверди SNS subscriptions за посиланнями в листах AWS.
- Перевір **Billing → Free Tier / Credits**. За заданого `ALERT_EMAILS` Terraform
  створює budget на $5; це лише повідомлення, не зупинка витрат. За порожнього
  списку budget не створюється. Доступність сервісів і ліміти залежать від плану
  акаунта; CloudFront, API Gateway, DynamoDB, логування та S3 можуть споживати
  кредити/генерувати витрати. Проєкт не гарантує рахунок $0.

## 2. GitHub

**Repository → Settings → Secrets and variables → Actions**.

### Secrets

| Назва | Значення |
|---|---|
| `AWS_ACCESS_KEY_ID` | Access key ID deployment user / тимчасової сесії |
| `AWS_SECRET_ACCESS_KEY` | Secret access key |
| `AWS_SESSION_TOKEN` | Лише для тимчасових credentials; для IAM user keys не задавати |

### Variables

| Назва | Значення |
|---|---|
| `AWS_REGION` | Необов'язково, default `eu-north-1`; один регіон для всього деплою |
| `TF_STATE_BUCKET` | Необов'язково, якщо bucket має іншу назву, ніж `restaurant-terraform-state-<account-id>` |
| `ALERT_EMAILS` | JSON-масив, наприклад `["you@example.com"]`; default `[]` |
| `EMAIL_FROM` | SES verified email для звітів |
| `S3_BUCKET` | Після Terraform: output `frontend_bucket_name` |
| `CLOUDFRONT_DISTRIBUTION_ID` | Після Terraform: output `cloudfront_distribution_id` |
| `BACKEND_URL` | Після backend deploy: `https://<api-id>.execute-api.eu-north-1.amazonaws.com/develop` |

У **Settings → Actions → General** дозволь workflows та використані сторонні
actions. Workflow Terraform запитує `pull-requests: write` для коментарів plan;
політика організації має це дозволяти. Deploy запускається лише з `main`.

## 3. Перший запуск — послідовно

1. Завантаж зміни в `main`. На порожньому AWS акаунті автоматичні push-запуски
   можуть впасти, бо workflows незалежні й інфраструктура ще не створена.
2. **Actions → Terraform → Run workflow → main**. Дочекайся успіху.
   У Summary скопіюй `frontend_bucket_name`, `cloudfront_distribution_id` у
   GitHub Variables. Збережи також `cloudfront_distribution_domain_name`.
3. **Actions → Deploy → Run workflow → main**, увімкни
   **Run syndicate deploy instead of update**. Це початкове створення backend.
   Workflow генерує Syndicate config для твого account ID, створює artifacts
   bucket за потреби й синхронізує deployment state із S3.
4. З Summary backend workflow скопіюй `BACKEND_URL` у GitHub Variables.
5. **Actions → Frontend → Run workflow → main**. Workflow перевірить код,
   збере production bundle, завантажить його в S3 і створить CloudFront invalidation.
6. Відкрий `https://<cloudfront_distribution_domain_name>`. Поширення змін
   CloudFront може зайняти кілька хвилин. Перевір запити до API у браузері.

Після першого запуску зміни backend у `main` викликають `syndicate update
--deploy-name dev`; frontend — build і upload; Terraform — apply. Якщо один commit
змінює залежні ресурси інфраструктури та код, дочекайся Terraform і за потреби
перезапусти Deploy/Frontend: автоматичного зв'язку `needs` між workflows немає.
Для додавання нових типів/екземплярів backend ресурсів перевір можливості Syndicate
`update`; для ресурсів, які він не створює, потрібен окремий цільовий deploy.

Frontend PR виконує CI build із тестовим URL без AWS credentials та без upload.
Terraform plan із credentials виконується лише для PR із цього репозиторію
(не fork і не Dependabot). Не запускай AWS plan для недовіреного коду.

## 4. Якщо ресурси вже існують

- Використовуй попередній **state bucket і ключ** `restaurant/terraform.tfstate`.
  Не застосовуй порожній state до вже створеної інфраструктури: спочатку імпортуй
  відповідні ресурси або віднови state. Перевір plan перед apply.
- Збережи попередній Syndicate artifacts bucket та його `.syndicate`/outputs.
  Для існуючого deployment `dev` запускай Deploy із вимкненим initial deploy.
  Інша назва deployment потребує зміни `DEPLOY_NAME` у workflow.
- Якщо bucket і ресурси створені в іншому регіоні, задай саме цей регіон;
  зміна `AWS_REGION` не переносить існуючі ресурси.

## 5. Типові помилки

| Помилка | Що перевірити |
|---|---|
| `InvalidClientTokenId`, `ExpiredToken` | Secrets, відповідність ключів, session token і термін сесії |
| `AccessDenied` для IAM/PassRole | Deployment policy, permissions boundary, SCP/обмеження лабораторії |
| `AccessDenied` для S3 public policy | Account-level Block Public Access, описаний вище |
| `NoSuchBucket` при Terraform init | State bucket створено в правильному акаунті й регіоні |
| Syndicate не знаходить deploy/output | Перший запуск із initial deploy; існуючий artifacts bucket і deployment name |
| Budget/SNS не надсилає листів | `ALERT_EMAILS` у JSON-форматі; підтвердження SNS |
| Порожній каталог ресторану | Нові DynamoDB таблиці порожні; потрібні початкові дані та зображення |

Infrastructure deploy не заповнює бізнес-дані ресторану автоматично.
