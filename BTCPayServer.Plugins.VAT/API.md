# BTCPay Server VAT Plugin - API Documentation

All store-scoped endpoints require [Greenfield API authentication](https://docs.btcpayserver.org/API/Greenfield/v1/) via API key or Bearer token.

Base URL: `https://your-btcpay-instance.com`

## Settings

### Get VAT Settings

```
GET /api/v1/stores/{storeId}/vat/settings
```

**Permission:** `btcpay.store.canviewstoresettings`

**Example:**

```bash
curl -s \
  -H "Authorization: token YOUR_API_KEY" \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/settings
```

**Response** `200 OK`

```json
{
  "storeId": "ABC123",
  "mode": 0,
  "homeCountry": "DE",
  "homeCountryName": "Germany",
  "vatNumber": "DE123456789",
  "enabled": true,
  "validateVIES": true,
  "homeCountryVATRate": 19.0
}
```

| Field | Type | Description |
|---|---|---|
| `mode` | `int` | `0` = Fixed (home country rate always), `1` = OSS (customer country rate) |
| `homeCountry` | `string` | ISO 3166-1 alpha-2 country code |
| `vatNumber` | `string?` | Business VAT registration number |
| `enabled` | `bool` | Whether VAT calculation is active |
| `validateVIES` | `bool` | Whether B2B VAT numbers are validated via VIES |
| `homeCountryVATRate` | `decimal` | Standard VAT rate for the home country |

---

### Update VAT Settings

```
PUT /api/v1/stores/{storeId}/vat/settings
```

**Permission:** `btcpay.store.canmodifystoresettings`

**Example — enable OSS mode for a German business:**

```bash
curl -s -X PUT \
  -H "Authorization: token YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "mode": 1,
    "homeCountry": "DE",
    "vatNumber": "DE123456789",
    "enabled": true,
    "validateVIES": true
  }' \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/settings
```

| Field | Type | Required | Description |
|---|---|---|---|
| `mode` | `int` | Yes | `0` = Fixed, `1` = OSS |
| `homeCountry` | `string` | Yes | ISO 3166-1 alpha-2 EU country code |
| `vatNumber` | `string?` | No | Business VAT registration number (max 20 chars) |
| `enabled` | `bool` | No | Enable/disable VAT calculation (default: `true`) |
| `validateVIES` | `bool` | No | Enable VIES validation for B2B reverse charge (default: `true`) |

**Response** `200 OK` — same shape as Get VAT Settings.

---

## VAT Calculation

### Calculate VAT (Preview)

Calculates VAT without creating an invoice. Useful for showing a price breakdown before checkout.

```
POST /api/v1/stores/{storeId}/vat/calculate
```

**Permission:** `btcpay.store.canviewstoresettings`

**Example — EU customer in France (OSS mode):**

```bash
curl -s -X POST \
  -H "Authorization: token YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.00,
    "currency": "EUR",
    "customerCountry": "FR"
  }' \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/calculate
```

```json
{
  "netAmount": 100.00,
  "vatRate": 20.0,
  "vatAmount": 20.00,
  "grossAmount": 120.00,
  "currency": "EUR",
  "countryCode": "FR",
  "countryName": "France",
  "reverseChargeApplied": false,
  "customerVATNumber": null,
  "modeApplied": 1
}
```

**Example — B2B with valid VAT number (reverse charge):**

```bash
curl -s -X POST \
  -H "Authorization: token YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 500.00,
    "currency": "EUR",
    "customerCountry": "FR",
    "customerVATNumber": "FR12345678901"
  }' \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/calculate
```

```json
{
  "netAmount": 500.00,
  "vatRate": 0.0,
  "vatAmount": 0.00,
  "grossAmount": 500.00,
  "currency": "EUR",
  "countryCode": "FR",
  "countryName": "France",
  "reverseChargeApplied": true,
  "customerVATNumber": "FR12345678901",
  "modeApplied": 1
}
```

**Example — non-EU customer (zero-rated export):**

```bash
curl -s -X POST \
  -H "Authorization: token YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 250.00,
    "currency": "USD",
    "customerCountry": "US"
  }' \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/calculate
```

```json
{
  "netAmount": 250.00,
  "vatRate": 0.0,
  "vatAmount": 0.00,
  "grossAmount": 250.00,
  "currency": "USD",
  "countryCode": "US",
  "countryName": "US",
  "reverseChargeApplied": false,
  "customerVATNumber": null,
  "modeApplied": 1
}
```

**Request fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `amount` | `decimal` | Yes | Net amount before VAT (must be > 0) |
| `currency` | `string` | Yes | Currency code, e.g. `"EUR"` |
| `customerCountry` | `string` | Yes | Customer's ISO 3166-1 alpha-2 country code |
| `customerVATNumber` | `string?` | No | Customer's VAT number for B2B reverse charge |

**VAT calculation logic:**

| Scenario | VAT Rate Applied |
|---|---|
| EU customer, Fixed mode | Home country rate |
| EU customer, OSS mode | Customer's country rate |
| EU B2B with valid VAT number (cross-border) | 0% (reverse charge) |
| Non-EU customer (any mode) | 0% (export, zero-rated) |

---

## Invoices

### Create Invoice with VAT

Creates a BTCPay Server invoice with VAT automatically calculated and recorded. The invoice amount is the **gross amount** (net + VAT). VAT details are stored in the invoice's `metadata.vatData` field for bookkeeping.

```
POST /api/v1/stores/{storeId}/vat/invoices
```

**Permission:** `btcpay.store.cancreateinvoice`

**Example — simple invoice:**

```bash
curl -s -X POST \
  -H "Authorization: token YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.00,
    "currency": "EUR",
    "customerCountry": "FR"
  }' \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/invoices
```

**Example — full invoice with all options:**

```bash
curl -s -X POST \
  -H "Authorization: token YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.00,
    "currency": "EUR",
    "customerCountry": "FR",
    "customerVATNumber": null,
    "buyerEmail": "customer@example.com",
    "buyerName": "Jane Doe",
    "orderId": "ORDER-001",
    "metadata": {
      "itemDescription": "Consulting services"
    },
    "checkout": {
      "expirationMinutes": 30,
      "paymentMethods": ["BTC-LN", "BTC"]
    }
  }' \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/invoices
```

| Field | Type | Required | Description |
|---|---|---|---|
| `amount` | `decimal` | Yes | Net amount before VAT |
| `currency` | `string` | Yes | Currency code |
| `customerCountry` | `string` | Yes | Customer's country code |
| `customerVATNumber` | `string?` | No | For B2B reverse charge |
| `buyerEmail` | `string?` | No | Customer email |
| `buyerName` | `string?` | No | Customer name |
| `orderId` | `string?` | No | Your order reference |
| `metadata` | `object?` | No | Additional key-value metadata |
| `redirectUrl` | `string?` | No | Post-payment redirect URL |
| `checkout.expirationMinutes` | `int?` | No | Invoice expiry (default: 15) |
| `checkout.paymentMethods` | `string[]?` | No | Allowed payment methods |

**Response** `201 Created`

```json
{
  "invoiceId": "KSAp4hxV7L...",
  "checkoutUrl": "/i/KSAp4hxV7L...",
  "status": "New",
  "vat": {
    "netAmount": 100.00,
    "vatRate": 20.0,
    "vatAmount": 20.00,
    "grossAmount": 120.00,
    "currency": "EUR",
    "countryCode": "FR",
    "countryName": "France",
    "reverseChargeApplied": false,
    "customerVATNumber": null,
    "modeApplied": 1
  },
  "createdAt": "2026-02-19T10:30:00Z",
  "expiresAt": "2026-02-19T11:00:00Z"
}
```

---

### Get Invoice VAT Details

Retrieve the VAT record for an invoice created through this plugin.

```
GET /api/v1/stores/{storeId}/vat/invoices/{invoiceId}
```

**Permission:** `btcpay.store.canviewinvoices`

**Example:**

```bash
curl -s \
  -H "Authorization: token YOUR_API_KEY" \
  https://btcpay.example.com/api/v1/stores/ABC123/vat/invoices/KSAp4hxV7L
```

**Response** `200 OK`

```json
{
  "invoiceId": "KSAp4hxV7L...",
  "storeId": "ABC123",
  "customerCountry": "FR",
  "vatRate": 20.0,
  "netAmount": 100.00,
  "vatAmount": 20.00,
  "grossAmount": 120.00,
  "currency": "EUR",
  "reverseChargeApplied": false,
  "customerVATNumber": null,
  "modeApplied": 1,
  "createdAt": "2026-02-19T10:30:00Z",
  "evidence": [
    {
      "type": "CustomerDeclaration",
      "countryCode": "FR",
      "confidence": 1.0,
      "collectedAt": "2026-02-19T10:30:00Z"
    }
  ]
}
```

---

## Utility Endpoints

### Get EU VAT Rates

Returns all current EU member state VAT rates.

```
GET /api/v1/vat/rates
```

**Authentication:** Greenfield API key or Bearer token required.

**Example:**

```bash
curl -s \
  -H "Authorization: token YOUR_API_KEY" \
  https://btcpay.example.com/api/v1/vat/rates
```

**Response** `200 OK`

```json
{
  "rates": [
    {
      "countryCode": "AT",
      "countryName": "Austria",
      "standardRate": 20.0,
      "reducedRate": 13.0,
      "superReducedRate": null
    },
    {
      "countryCode": "DE",
      "countryName": "Germany",
      "standardRate": 19.0,
      "reducedRate": 7.0,
      "superReducedRate": null
    },
    {
      "countryCode": "FR",
      "countryName": "France",
      "standardRate": 20.0,
      "reducedRate": 5.5,
      "superReducedRate": 2.1
    }
  ]
}
```

---

### Validate VAT Number (VIES)

Validates a European VAT number against the EU VIES database.

```
GET /api/v1/vat/validate/{vatNumber}
```

**Authentication:** Greenfield API key or Bearer token required.

**Example:**

```bash
curl -s \
  -H "Authorization: token YOUR_API_KEY" \
  https://btcpay.example.com/api/v1/vat/validate/DE123456789
```

**Response** `200 OK`

```json
{
  "vatNumber": "DE123456789",
  "countryCode": "DE",
  "isValid": true,
  "name": "Example GmbH",
  "address": "Musterstr. 1, 10115 Berlin",
  "validationDate": "2026-02-19T10:30:00Z",
  "errorMessage": null
}
```

**Example — invalid number:**

```bash
curl -s \
  -H "Authorization: token YOUR_API_KEY" \
  https://btcpay.example.com/api/v1/vat/validate/XX000000000
```

```json
{
  "vatNumber": "XX000000000",
  "countryCode": "XX",
  "isValid": false,
  "name": null,
  "address": null,
  "validationDate": "2026-02-19T10:30:00Z",
  "errorMessage": "Invalid VAT number format"
}
```

---

## Error Responses

All endpoints return errors in this format:

```json
{
  "message": "Description of what went wrong"
}
```

| Status | Meaning |
|---|---|
| `400` | Invalid request (validation error, invalid country code) |
| `401` | Missing or invalid authentication |
| `403` | Insufficient permissions |
| `404` | Settings or record not found |
