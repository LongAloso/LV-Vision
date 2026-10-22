using IdentityService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Domain.Interfaces
{
    public interface IUserSettingRepository
    {
        Task CreateAsync(UserSetting role, CancellationToken cancellationToken = default);
        Task UpdateAsync(UserSetting role, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<UserSetting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSetting>> SearchAsync(
            string userId,
            string? keyword, //dashboardSpc, pageLayout, measureForm
            CancellationToken cancellationToken = default);
    }
}
