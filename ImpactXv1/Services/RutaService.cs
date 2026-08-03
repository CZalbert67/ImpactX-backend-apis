using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class RutaService : IRutaService
{
    private readonly IRutaRepository _rutaRepository;

    public RutaService(IRutaRepository rutaRepository)
    {
        _rutaRepository = rutaRepository;
    }

    public async Task<List<RutaDto>> GetFrequentAsync(Guid usuarioId)
    {
        var rutas = await _rutaRepository.GetFrequentByUserAsync(usuarioId);
        return rutas.Select(MapToDto).ToList();
    }

    public async Task<List<RutaDto>> GetHistoryAsync(Guid usuarioId)
    {
        var rutas = await _rutaRepository.GetHistoryByUserAsync(usuarioId);
        return rutas.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<RutaDto>> GetFrequentPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _rutaRepository.GetFrequentByUserPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<RutaDto>
        {
            Items = page.Items.Select(MapToDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<PagedResult<RutaDto>> GetHistoryPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _rutaRepository.GetHistoryByUserPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<RutaDto>
        {
            Items = page.Items.Select(MapToDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<RutaDto> CreateAsync(Guid usuarioId, CreateRutaRequest request)
    {
        var ruta = new Ruta
        {
            UsuarioId = usuarioId,
            Nombre = request.Nombre,
            Etiqueta = request.Etiqueta,
            Nota = request.Nota,
            Origen = request.Origen,
            OrigenLat = request.OrigenLat,
            OrigenLng = request.OrigenLng,
            Destino = request.Destino,
            DestinoLat = request.DestinoLat,
            DestinoLng = request.DestinoLng,
            DistanciaKm = request.DistanciaKm,
            DuracionEstimadaMin = request.DuracionEstimadaMin,
            EsFrecuente = request.EsFrecuente,
            CreadoEn = DateTime.UtcNow,
        };

        await _rutaRepository.AddAsync(ruta);
        return MapToDto(ruta);
    }

    public async Task<RutaDto> UpdateAsync(Guid usuarioId, Guid id, UpdateRutaRequest request)
    {
        var ruta = await _rutaRepository.GetByIdAsync(usuarioId, id)
            ?? throw new NotFoundException("Ruta no encontrada.");

        if (ruta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar esta ruta.");

        if (request.Nombre is not null) ruta.Nombre = request.Nombre;
        if (request.Etiqueta is not null) ruta.Etiqueta = request.Etiqueta;
        if (request.Nota is not null) ruta.Nota = request.Nota;
        if (request.Origen is not null) ruta.Origen = request.Origen;
        if (request.OrigenLat.HasValue) ruta.OrigenLat = request.OrigenLat.Value;
        if (request.OrigenLng.HasValue) ruta.OrigenLng = request.OrigenLng.Value;
        if (request.Destino is not null) ruta.Destino = request.Destino;
        if (request.DestinoLat.HasValue) ruta.DestinoLat = request.DestinoLat.Value;
        if (request.DestinoLng.HasValue) ruta.DestinoLng = request.DestinoLng.Value;
        if (request.DistanciaKm.HasValue) ruta.DistanciaKm = request.DistanciaKm.Value;
        if (request.DuracionEstimadaMin.HasValue) ruta.DuracionEstimadaMin = request.DuracionEstimadaMin.Value;
        if (request.EsFrecuente.HasValue) ruta.EsFrecuente = request.EsFrecuente.Value;

        await _rutaRepository.UpdateAsync(ruta);
        return MapToDto(ruta);
    }

    public async Task DeleteAsync(Guid usuarioId, Guid id)
    {
        var ruta = await _rutaRepository.GetByIdAsync(usuarioId, id)
            ?? throw new NotFoundException("Ruta no encontrada.");

        if (ruta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para eliminar esta ruta.");

        await _rutaRepository.DeleteAsync(ruta);
    }

    public async Task<RutaDto> SelectTodayAsync(Guid usuarioId, SelectTodayRequest request)
    {
        var ruta = await _rutaRepository.GetByIdAsync(usuarioId, request.RutaId)
            ?? throw new NotFoundException("Ruta no encontrada.");

        if (ruta.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para seleccionar esta ruta.");

        var currentSelected = await _rutaRepository.GetSelectedTodayAsync(usuarioId);
        if (currentSelected is not null)
        {
            currentSelected.SeleccionadaHoy = false;
            await _rutaRepository.UpdateAsync(currentSelected);
        }

        ruta.SeleccionadaHoy = true;
        ruta.UsadaEn = DateTime.UtcNow;
        await _rutaRepository.UpdateAsync(ruta);

        return MapToDto(ruta);
    }

    private static RutaDto MapToDto(Ruta r) => new()
    {
        Id = r.Id,
        Nombre = r.Nombre,
        Etiqueta = r.Etiqueta,
        Nota = r.Nota,
        Origen = r.Origen,
        OrigenLat = r.OrigenLat,
        OrigenLng = r.OrigenLng,
        Destino = r.Destino,
        DestinoLat = r.DestinoLat,
        DestinoLng = r.DestinoLng,
        DistanciaKm = r.DistanciaKm,
        DuracionEstimadaMin = r.DuracionEstimadaMin,
        EsFrecuente = r.EsFrecuente,
        SeleccionadaHoy = r.SeleccionadaHoy,
        CreadoEn = r.CreadoEn,
        UsadaEn = r.UsadaEn,
    };
}
