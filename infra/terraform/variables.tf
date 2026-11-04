variable "aws_region" {
  description = "AWS region for all resources except CloudFront/ACM globals"
  type        = string
  default     = "eu-north-1"
}

variable "account_id" {
  description = "AWS account ID"
  type        = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.account_id))
    error_message = "account_id must be a 12-digit AWS account ID."
  }
}

variable "alert_emails" {
  description = "Email addresses subscribed to CloudWatch alerts and Budget notifications. In CI set via the TF_VAR_alert_emails env var (GitHub Actions variable ALERT_EMAILS); locally via a gitignored terraform.tfvars. Value must be HCL/JSON list syntax, e.g. [\"you@example.com\"]."
  type        = list(string)
  default     = []
}
