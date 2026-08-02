using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(Guid id);
    Task<Usuario?> GetByCorreoAsync(string correo);
    Task<Usuario?> GetByUsernameAsync(string username);
    Task<Usuario?> GetByPublicProfileIdAsync(string publicProfileId);
    Task<List<Usuario>> SearchAsync(string query, string? by = null);
    Task<bool> ExistsByCorreoAsync(string correo);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByPublicProfileIdAsync(string publicProfileId);
    Task<bool> ExistsByUsernameIncludingHistoryAsync(string username);
    Task<bool> ExistsByUsernameHistoryExcludingUsuarioAsync(string username, Guid usuarioId);
    Task AddAsync(Usuario usuario);
    Task UpdateAsync(Usuario usuario);
    Task DeleteAsync(Usuario usuario);
}
