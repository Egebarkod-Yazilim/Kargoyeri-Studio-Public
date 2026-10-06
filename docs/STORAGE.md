# Pluggable Storage (P5-#8)

Kargoyeri Studio dosya kalici yazimlari icin `IBlobStorage` soyutlamasini kullanir:

| Backend                   | Use case                              |
|---------------------------|----------------------------------------|
| `LocalFileBlobStorage`    | Default, dev + tek-instance prod      |
| `S3BlobStorage`           | AWS S3 / MinIO / R2 / DO Spaces / Wasabi |
| `AzureBlobStorage` (skel) | Azure deployment (production'da SDK'ya swap) |

## Konfigurasyon

```json
"Studio": {
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "https://s3.eu-central-1.amazonaws.com",
      "Region": "eu-central-1",
      "AccessKey": "AKIA...",
      "SecretKey": "...",
      "Bucket": "kargoyeri-studio-prod",
      "UsePathStyle": false
    }
  }
}
```

ENV variable formati:
```
Studio__Storage__Provider=S3
Studio__Storage__S3__Bucket=kargoyeri-studio-prod
Studio__Storage__S3__AccessKey=...
Studio__Storage__S3__SecretKey=...
```

## Kullanim

```csharp
public class MyService(IBlobStorage storage)
{
    public async Task SaveLabelAsync(string tenantKey, string trackingNumber, byte[] pdfBytes)
    {
        await storage.PutAsync(
            container: "labels",
            key: $"{tenantKey}/{trackingNumber}.pdf",
            data: pdfBytes,
            contentType: "application/pdf");
    }
}
```

## Multi-region nasil cozuluyor?

Local backend her instance'in kendi diskine yazar — multi-region OLMAZ.
S3/Azure cross-region replication ile bolge degisikligine ragmen aynı veri saglar.

Tenant metrics, signup-pending, certification reports gibi yazimlar
P5-#8 sonrasinda IBlobStorage uzerinden gecirildiginde cross-region calisir.

## S3-compatible Backends

### MinIO (self-hosted)
```yaml
# docker-compose.yml
minio:
  image: minio/minio:latest
  command: server /data --console-address ":9001"
  ports: ["9000:9000", "9001:9001"]
  environment:
    MINIO_ROOT_USER: kargoyeri
    MINIO_ROOT_PASSWORD: <strong-password>
  volumes: ["minio-data:/data"]
```
Config:
```json
"S3": {
  "Endpoint": "http://minio:9000",
  "Region": "us-east-1",
  "Bucket": "kargoyeri-studio",
  "UsePathStyle": true
}
```

### Cloudflare R2
```json
"S3": {
  "Endpoint": "https://<account-id>.r2.cloudflarestorage.com",
  "Region": "auto",
  "Bucket": "kargoyeri-studio",
  "UsePathStyle": true
}
```

### DigitalOcean Spaces
```json
"S3": {
  "Endpoint": "https://fra1.digitaloceanspaces.com",
  "Region": "fra1",
  "Bucket": "kargoyeri-studio",
  "UsePathStyle": false
}
```

## Production'a gec

1. AWSSDK.S3 NuGet ekle (daha tam ListObjectsV2 + multipart upload icin)
2. `S3BlobStorage` icindeki ListAsync skeleton'unu SDK ile degistir
3. `AzureBlobStorage` icin Azure.Storage.Blobs paketini ekleyip implementasyonu yaz
4. Pre-signed URL'leri butun callsite'larda manuel rebuild yerine SDK'nin builder'ini kullan

## Migration: local FS -> S3

Mevcut local dosyalari S3'e tasi:
```bash
aws s3 sync ./blobs s3://kargoyeri-studio-prod/ --acl private
```
Sonra `Studio:Storage:Provider` -> `"S3"` set, restart.
