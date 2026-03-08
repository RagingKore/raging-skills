# Deploying Astro to AWS and Google Cloud

## AWS S3 + CloudFront

### S3 Bucket Setup

```bash
# Create bucket
aws s3 mb s3://my-astro-site

# Enable static website hosting
aws s3 website s3://my-astro-site \
  --index-document index.html \
  --error-document 404.html

# Sync build output
aws s3 sync dist/ s3://my-astro-site --delete
```

### Bucket Policy

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PublicReadGetObject",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::my-astro-site/*"
    }
  ]
}
```

### CloudFront Distribution

Key settings:

- Origin: S3 bucket website endpoint
- Default root object: `index.html`
- Custom error response: 404 to `/404.html`
- Cache behavior: Use cache policy for static assets

### Cache Invalidation on Deploy

```bash
aws cloudfront create-invalidation \
  --distribution-id E1234567890 \
  --paths "/*"
```

## GCS + Cloud CDN

Alternative to Firebase Hosting using Google Cloud Storage with Cloud CDN.

### Bucket Setup

```bash
# Create bucket with uniform access
gsutil mb -l us-central1 gs://my-astro-site
gsutil uniformbucketlevelaccess set on gs://my-astro-site

# Enable public access
gsutil iam ch allUsers:objectViewer gs://my-astro-site

# Configure as static site
gsutil web set -m index.html -e 404.html gs://my-astro-site
```

### Deploy Script

```bash
# Build and sync
npm run build
gsutil -m rsync -r -d dist/ gs://my-astro-site

# Set cache headers for assets
gsutil -m setmeta -h "Cache-Control:public, max-age=31536000" \
  gs://my-astro-site/_astro/**
```

### Cloud CDN Setup

1. Create a backend bucket in Cloud Console
2. Point it to your GCS bucket
3. Create a load balancer with the backend bucket
4. Enable Cloud CDN on the backend bucket
5. Configure SSL certificate for HTTPS

```bash
# Invalidate CDN cache after deploy
gcloud compute url-maps invalidate-cdn-cache my-lb \
  --path "/*" --async
```

## When to Use GCS + CDN vs Firebase

| Scenario                           | Recommendation     |
|------------------------------------|--------------------|
| Simple static site                 | Firebase Hosting   |
| Need fine-grained cache control    | GCS + CDN          |
| Already using GCP extensively      | GCS + CDN          |
| Want simplest setup                | Firebase Hosting   |
| Need custom CDN configuration      | GCS + CDN          |
