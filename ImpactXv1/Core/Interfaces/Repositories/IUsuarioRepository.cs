using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(Guid id);
    Task<Usuario?> GetByCorreoAsync(string correo);
    Task<Usuario?> GetByUsernameAsync(string username);
    Task<List<Usuario>> SearchAsync(string query);
    Task<bool> ExistsByCorreoAsync(string correo);
    Task<bool> ExistsByUsernameAsync(string username);
    Task AddAsync(Usuario usuario);
    Task UpdateAsync(Usuario usuario);
    Task DeleteAsync(Usuario usuario);
}
