using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ScreenTimeWin.Data;

public partial class DataRepository
{
    // Simple Key-Value storage for settings like PIN
    // Ideally we should have a Settings table, but we can reuse or add one.
    // For now, let's use a file or a new table. 
    // Let's add a GlobalSettings table to DB context.
    
    private const string PinKey = "AdminPinHash";

    public async Task<bool> VerifyPinAsync(string pin)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var hashEntry = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == PinKey);
        
        if (hashEntry == null) return true; // No PIN set means verification passed (or handled by UI state)
        
        var inputHash = HashPin(pin);
        return inputHash == hashEntry.Value;
    }

    public async Task<bool> SetPinAsync(string oldPin, string newPin)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var hashEntry = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == PinKey);
        
        if (hashEntry != null)
        {
            // Verify old pin
            var oldHash = HashPin(oldPin);
            if (oldHash != hashEntry.Value) return false;
            
            if (string.IsNullOrEmpty(newPin))
            {
                // Remove PIN
                context.GlobalSettings.Remove(hashEntry);
            }
            else
            {
                // Update PIN
                hashEntry.Value = HashPin(newPin);
            }
        }
        else
        {
            // Set new PIN (ignore oldPin if not set previously)
            if (!string.IsNullOrEmpty(newPin))
            {
                context.GlobalSettings.Add(new Core.Entities.GlobalSetting { Key = PinKey, Value = HashPin(newPin) });
            }
        }
        
        await context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> HasPinAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GlobalSettings.AnyAsync(s => s.Key == PinKey);
    }

    private string HashPin(string pin)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }
}
