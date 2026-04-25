# HnBestStories

REST API built with ASP.NET Core to return the best `N` Hacker News stories ordered by score descending.

The solution is intentionally production-oriented but focused on the real challenge: external API integration, efficient caching, resilience, safe error handling, and clear tests.

## Challenge summary

The API exposes:

```http
GET /api/stories/best?n=10
```

It calls the Hacker News Firebase API:

```http
GET https://hacker-news.firebaseio.com/v0/beststories.json
GET https://hacker-news.firebaseio.com/v0/item/{id}.json
```

And returns:

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

## Technology decisions

- .NET 10 LTS
- ASP.NET Core
- Clean Architecture
- Lightweight domain model
- Redis distributed cache
- HttpClientFactory
- Polly retry and circuit breaker policies
- Serilog structured logging
- Seq for local log visualization
- Docker and docker-compose
- xUnit, FluentAssertions, WireMock.Net, Testcontainers

## Why .NET 10 LTS

.NET 10 LTS was selected because it is the latest long-term support version of .NET, aligned with modern cloud-ready ASP.NET Core applications.

If your local environment does not support .NET 10 yet, you can switch the target framework to .NET 8 LTS by changing `Directory.Build.props`:

```xml
<TargetFramework>net8.0</TargetFramework>
```

You may also need to downgrade package versions from `10.0.0` to compatible `8.x` or `9.x` versions.

## Architecture

```text
src/
  HnBestStories.Api
  HnBestStories.Application
  HnBestStories.Domain
  HnBestStories.Infrastructure
  HnBestStories.Worker

tests/
  HnBestStories.UnitTests
  HnBestStories.IntegrationTests
```

### Api

Responsible for:

- HTTP endpoints
- request validation
- ProblemDetails error responses
- Swagger/OpenAPI
- rate limiting
- health checks
- correlation id middleware
- security headers
- dependency injection

### Application

Responsible for:

- `GetBestStories` use case
- orchestration between cache and Hacker News gateway
- filtering invalid stories
- sorting by score descending
- fallback behavior when stale cache exists

### Domain

Contains a small expressive model:

- `Story`
- `StoryId`
- `DomainException`

This API is read-only and integration-focused, so the domain model intentionally avoids artificial aggregates or unnecessary complexity.

### Infrastructure

Responsible for:

- Hacker News typed HttpClient
- Redis cache implementation
- Polly resilience policies
- strongly typed options
- JSON serialization

### Worker

Optional cache warmer.

The worker periodically refreshes the top 500 stories and stores them in Redis.

The HTTP endpoint does not depend on the worker. If the worker is unavailable, the API still works through cache-aside behavior.

## Cache strategy

Redis stores a single sorted top 500 list:

```text
hn:beststories:top500
```

The API slices this cached list in memory depending on `n`.

This avoids creating different cache keys for every `n` value.

The cached object has two lifetimes:

- fresh TTL: data is considered fresh
- stale TTL: data may still be returned if Hacker News is unavailable

## Resilience

The solution uses:

- `HttpClientFactory`
- retry with exponential backoff
- circuit breaker
- timeout configured on HttpClient
- limited concurrency when fetching item details
- partial failure tolerance

If one story fails, the API logs a warning and continues with the remaining stories.

If Hacker News is unavailable and stale cache exists, stale cache is returned.

If Hacker News is unavailable and no cache exists, the API returns `503` with `ProblemDetails`.

## Security

Authentication was intentionally not added because the API exposes only public Hacker News data.

Security is focused on:

- input validation
- rate limiting
- safe error responses
- no stack traces in responses
- security headers
- explicit CORS configuration
- Redis isolated inside Docker network
- non-root containers
- `.dockerignore`
- no secrets hardcoded

## Health checks

```http
GET /health/live
GET /health/ready
```

`/health/live` verifies the API process is alive.

`/health/ready` verifies required dependencies such as Redis.

Hacker News is not called by default from health checks to avoid unnecessary external traffic.

## Running locally

Prerequisites:

- .NET 10 SDK
- Redis running locally on `localhost:6379`

Run the API:

```bash
dotnet run --project src/HnBestStories.Api/HnBestStories.Api.csproj
```

Open Swagger in development:

```http
https://localhost:5001/swagger
```

Call the endpoint:

```bash
curl "https://localhost:5001/api/stories/best?n=10"
```

## Running with Docker

```bash
docker compose up --build
```

API:

```http
http://localhost:8080/api/stories/best?n=10
```

Seq:

```http
http://localhost:8081
```

## Running tests

```bash
dotnet test
```

The integration tests use WireMock.Net to mock Hacker News and Testcontainers for Redis.
Docker must be running when executing the integration tests.

## Assumptions

- `descendants` is used as `commentCount`.
- `time` is converted from Unix timestamp to UTC `DateTimeOffset`.
- `n` maximum is 500 because Hacker News returns up to 500 best stories.
- If `url` is missing, the Hacker News discussion URL is used as fallback.
- Deleted, dead, invalid, and non-story items are ignored.
- The background worker is optional and not required for the HTTP endpoint to work.
- Authentication is not implemented because the API exposes public data only.
