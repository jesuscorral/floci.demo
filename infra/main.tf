resource "aws_s3_bucket" "documents" {
  bucket = var.bucket_name
}

resource "aws_sqs_queue" "document_events" {
  name                       = var.queue_name
  message_retention_seconds  = 86400
  visibility_timeout_seconds = 30
}
