using System.Diagnostics;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mp4_merger;

public class HostedService : IHostedService
{
    private readonly ILogger<HostedService> _logger;

    public HostedService(ILogger<HostedService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AppDomain.MonitoringIsEnabled = true;

        Console.Write("Root folder path: ");

        var rootPath = Console.ReadLine();
        
        Console.Clear();

        _logger.LogInformation($"{nameof(HostedService)} has started.");
        _logger.LogInformation("Root folder path: {rootPath}", rootPath);

        if (rootPath == null)
            throw new ArgumentNullException(nameof(rootPath), "Path cannot be empty.");

        var directories = Directory.GetDirectories(rootPath);
        foreach (var directory in directories)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested) break;

                await WorkDirectoryAsync(directory, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Took: {Took:#,###} ms \t Allocated: {Allocated:#,#} kb \t Peak Working Set: {PeakWorkingSet:#,#} kb \t Gen 0: {Gen0} \t Gen 1: {Gen1} \t Gen 2: {Gen2}",
            AppDomain.CurrentDomain.MonitoringTotalProcessorTime.TotalMilliseconds,
            AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize / 1024,
            Process.GetCurrentProcess().PeakWorkingSet64 / 1024,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2));
    }

    private async Task WorkDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var di = new DirectoryInfo(directoryPath);

        _logger.LogDebug("Current folder: {folder} and directory: {directory}", di.Name, directoryPath);

        var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories).OrderBy(o => o, new NaturalStringComparer()).ToList();

        try
        {
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "./ffmpeg", TemporaryFilesFolder = "/temp" });
            Directory.CreateDirectory(GlobalFFOptions.Current.TemporaryFilesFolder);

            var conversions = files.Select(videoPath =>
            {
                FFMpegHelper.ConversionSizeExceptionCheck(FFProbe.Analyse(videoPath));
                var destinationFolder = Path.Combine(GlobalFFOptions.Current.TemporaryFilesFolder, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(destinationFolder);
                var destinationPath = Path.Combine(destinationFolder, $"{Path.GetFileNameWithoutExtension(videoPath)}{FileExtension.Ts}");
                FFMpeg.Convert(videoPath, destinationPath, VideoType.Ts);
                return destinationPath;
            }).ToArray();

            try
            {
                await FFMpegArguments
                    .FromConcatInput(conversions)
                    .OutputToFile($"{di.Parent}\\{di.Name}.mp4", true, options => options.CopyChannel().WithBitStreamFilter(Channel.Audio, Filter.Aac_AdtstoAsc))
                    .NotifyOnProgress(percent => { _logger.LogInformation($"{di.Name}: {percent}"); })
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously();

                di.Delete(true);
            }
            finally
            {
                if (Directory.Exists(GlobalFFOptions.Current.TemporaryFilesFolder)) Directory.Delete(GlobalFFOptions.Current.TemporaryFilesFolder, true);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation cancelled while running FFmpeg.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot run FFmpeg.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(HostedService)} is shutting down.");

        await Task.CompletedTask;
    }
}