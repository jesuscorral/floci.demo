output "bucket_name" {
  value = aws_s3_bucket.documents.bucket
}

output "queue_url" {
  value = aws_sqs_queue.document_events.url
}

output "queue_arn" {
  value = aws_sqs_queue.document_events.arn
}
