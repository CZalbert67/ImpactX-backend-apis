using System.Net;
using System.Text.Json;
using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado: {Message}", ex.Message);

            context.Response.ContentType = "application/json";
            var (statusCode, mensaje) = MapException(ex);

            context.Response.StatusCode = (int)statusCode;

            var response = new ErrorResponse
            {
                StatusCode = (int)statusCode,
                Mensaje = mensaje,
                Detalle = context.RequestServices
                    .GetService<IWebHostEnvironment>()?.IsDevelopment() == true
                    ? ex.Message
                    : null,
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }

    private static (HttpStatusCode code, string message) MapException(Exception ex)
    {
        return ex switch
        {
            NotFoundException => (HttpStatusCode.NotFound, ex.Message),
            ConflictException => (HttpStatusCode.Conflict, ex.Message),
            ForbiddenException => (HttpStatusCode.Forbidden, ex.Message),
            BadRequestException => (HttpStatusCode.BadRequest, ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "No tienes permisos para esta acción."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "El recurso solicitado no fue encontrado."),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "Ocurrió un error interno en el servidor.")
        };
    }
}