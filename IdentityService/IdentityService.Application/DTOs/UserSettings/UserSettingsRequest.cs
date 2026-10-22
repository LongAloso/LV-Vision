using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Application.DTOs.UserSettings
{
    public class UserSettingsRequest
    { 
        public string? UserId { get; set; }
        public string? keyword { get; set; } = string.Empty;
    }
}
