using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecoveryService.Models;
using System.Diagnostics;
using System.Security.Principal;


namespace RecoveryService.Controllers
{

    [ApiController]
    [Route("recover")]
    public class RecoveryController : ControllerBase
    {
        private readonly ILogger<RecoveryController> _logger;

        public RecoveryController(ILogger<RecoveryController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Recover(RecoveryRequest request)
        {
            if (request == null) return BadRequest("Request body required");
            if (string.IsNullOrWhiteSpace(request.Target)) return BadRequest("Target missing");

            // Log caller identity for diagnostics
            var identity = WindowsIdentity.GetCurrent();
            _logger.LogInformation("Recover called by {Name} (IsAuthenticated={IsAuthenticated})", identity?.Name, identity?.IsAuthenticated);

            bool isAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                _logger.LogWarning("Process is not running as Administrator. Recovery will not be performed.");
                return Problem(detail: "Server process is not running as Administrator. Cannot perform IIS management actions. Run the app under an account with IIS privileges or use a privileged agent.", statusCode: 500);
            }

            string psCommand = request.Action switch
            {
                RecoveryAction.RestartAppPool =>
                    $"Restart-WebAppPool \"{request.Target}\"",
                RecoveryAction.RestartWebsite =>
                    $"Stop-Website \"{request.Target}\"; Start-Website \"{request.Target}\"",
                RecoveryAction.FullRecovery =>
                    $"Restart-WebAppPool \"{request.Target}\"; " +
                    $"Stop-Website \"{request.Target}\"; " +
                    $"Start-Website \"{request.Target}\"",
                _ => null
            };


            if (psCommand == null) return BadRequest("Invalid action");


            try
            {
                var result = RunPowerShell(psCommand);
                _logger.LogInformation("PowerShell exit {ExitCode}. StdOut: {StdOut} StdErr: {StdErr}", result.ExitCode, result.Output, result.Error);

                if (result.ExitCode != 0)
                    return Problem(detail: $"PowerShell failed: {result.Error}", statusCode: 500);

                return Ok("Recovery executed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery failed");
                return Problem(detail: "Recovery failed. See server logs for details.", statusCode: 500);
            }
        }

        private (int ExitCode, string Output, string Error) RunPowerShell(string command)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Import-Module WebAdministration; {command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                throw new Exception("Failed to start PowerShell process");

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output, error);
        }
    }

}
