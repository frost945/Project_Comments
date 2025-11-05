SPA Comments

A single-page application for posting comments with cascading replies. Built with a focus on secure input, an object-oriented approach, and easy data sorting.

Features

- Publishing comments and replies to comments (unlimited nesting)
- Storing comments and user data in a relational database
- Tabular display of root comments with sorting:
by User Name, Email, and date added (both directions)
- Sending and downloading images and text files
- Global error handling
- Swagger documentation
- Pagination: 25 posts per page
- Data validation on both the client and server
- Check for correct tag closing (valid XHTML)
- XSS and SQL Injection protection

Technologies

- NET 9: Latest version of .NET for modern
- ASP.NET Core Web API: For building RESTful services
- Entity Framework Core: ORM for database operations
- MS SQL Server
- Frontend: SPA (JavaScript, HTML, CSS)
- Swagger: For API documentation

Project Structure

Comments.Api — Web API controllers and middleware
Comments.Application — Business logic, processing images and text files
Comments.Models — entities and enums
Comments.wwwroot — SPA UI

Requirements

.NET 9 SDK
SQL Server for local development without Docker
Docker and Docker Compose (for containerized deployment)

Setup Instructions

1. Clone the repository: https://github.com/frost945/Project_Comments
2. Uncomment connection string в appsettings.json/appsettings.Development.json for Docker
3. docker-compose up -d
Access to the application Frontend: http://localhost:5000

API Endpoints

- POST /Comment: Create a new comment
- GET /Comment/parent: Get all parent comments
- GET /Comment/children/{parentId}: Get all children comments