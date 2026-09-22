# ParkFlow

ParkFlow is a parking management system designed to simplify and organize parking operations such as slot tracking, vehicle records, and parking flow management. It is built using ASP.NET Core with Clean Architecture principles.

## Features
- Manage parking data
- Clean Architecture structure
- PostgreSQL database integration
- REST API backend

## Tech Stack
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL

## Architecture
- Domain – Core entities
- Application – Business logic
- Infrastructure – Database access
- API – Controllers

## Notes
A simple backend project focused on Clean Architecture principles.

## Runtime Configuration
- `ASPNETCORE_URLS`: Optional server bind URL(s). If omitted, the API defaults to `http://0.0.0.0:5000`.
- `Cors__AllowedOrigins`: Optional comma-separated/array configuration for trusted browser origins (for example, admin web app hosts).  
  - When set, CORS allows only the configured origins with credentials support.
  - When not set, CORS falls back to non-credentialed `AllowAnyOrigin` for compatibility.
