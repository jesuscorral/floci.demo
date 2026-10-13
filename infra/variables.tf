variable "floci_endpoint" {
  description = "Endpoint de Floci"
  type        = string
  default     = "http://localhost:4566"
}

variable "region" {
  description = "Región de AWS (ficticia en local)"
  type        = string
  default     = "us-east-1"
}

variable "bucket_name" {
  description = "Nombre del bucket donde se guardan los documentos"
  type        = string
  default     = "floci-demo-documents"
}

variable "queue_name" {
  description = "Nombre de la cola donde se publican los eventos de documento"
  type        = string
  default     = "floci-demo-document-events"
}
