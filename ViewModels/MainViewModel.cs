using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using VSCleaner.Models;
using System.Diagnostics;

namespace VSCleaner.ViewModels
{
    public class MainViewModel : DependencyObject
    {
        // 1. 프로퍼티
        public ObservableCollection<FolderItem> TargetFolders { get; } = new ObservableCollection<FolderItem>();

        public string StatusMessage
        {
            get { return (string)GetValue(StatusMessageProperty); }
            set { SetValue(StatusMessageProperty, value); }
        }
        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register("StatusMessage", typeof(string), typeof(MainViewModel), new PropertyMetadata("준비됨"));

        public string SelectedPath
        {
            get { return (string)GetValue(SelectedPathProperty); }
            set { SetValue(SelectedPathProperty, value); }
        }
        public static readonly DependencyProperty SelectedPathProperty =
            DependencyProperty.Register("SelectedPath", typeof(string), typeof(MainViewModel), new PropertyMetadata("선택된 경로 없음"));

        // 2. 커맨드
        public ICommand ScanCommand => new RelayCommand(async _ => await ScanFoldersAsync());
        public ICommand ClearCommand => new RelayCommand(_ => ClearFolders());

        // 3. 스캔 로직 (스레드 안전 버전)
        private async Task ScanFoldersAsync()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                SelectedPath = dialog.FolderName;
                StatusMessage = "스캔 중...";

                // UI 스레드에서 리스트 초기화
                TargetFolders.Clear();

                // 백그라운드에서 모든 데이터를 먼저 수집 (UI 요소 접근 금지)
                var itemsFound = await Task.Run(() =>
                {
                    var tempList = new List<FolderItem>();

                    // (1) VS 프로젝트 찌꺼기 스캔
                    try
                    {
                        var directories = Directory.GetDirectories(SelectedPath, "*", SearchOption.AllDirectories)
                            .Where(d => d.EndsWith("\\bin") || d.EndsWith("\\obj") || d.EndsWith("\\.vs"));

                        foreach (var dir in directories)
                        {
                            long size = GetDirectorySize(dir);
                            tempList.Add(new FolderItem { Path = dir, SizeBytes = size });
                        }
                    }
                    catch { /* 권한 없는 폴더 등 예외 무시 */ }

                    // (2) 디스코드 캐시 경로 추가
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string[] discordPaths = {
                        Path.Combine(appData, "discord", "Cache"),
                        Path.Combine(appData, "discord", "Code Cache"),
                        Path.Combine(appData, "discord", "GPUCache")
                    };

                    foreach (var path in discordPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            long size = GetDirectorySize(path);
                            if (size > 0) tempList.Add(new FolderItem { Path = path, SizeBytes = size });
                        }
                    }
                    return tempList;
                });

                // 수집 완료 후 UI 스레드에서 안전하게 리스트 업데이트
                foreach (var item in itemsFound)
                {
                    TargetFolders.Add(item);
                }
                SelectedPath = $"{dialog.FolderName} (외 디스코드 캐시 포함)";
                StatusMessage = $"스캔 완료: {TargetFolders.Count}개의 항목 발견";
            }
        }

        // 4. 청소 로직
        private void ClearFolders()
        {
            if (TargetFolders.Count == 0)
            {
                MessageBox.Show("청소할 폴더나 파일이 없습니다.");
                return;
            }

            var result = MessageBox.Show("정말로 삭제하시겠습니까?", "최종 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            // 디스코드 종료 확인
            if (Process.GetProcessesByName("discord").Length > 0)
            {
                var procResult = MessageBox.Show("디스코드가 실행 중입니다. 종료하고 계속할까요?", "프로세스 종료", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (procResult == MessageBoxResult.Yes)
                {
                    foreach (var proc in Process.GetProcessesByName("discord"))
                    {
                        try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                    }
                }
            }

            int successCount = 0;
            int failCount = 0;
            var itemsToDelete = TargetFolders.ToList();

            foreach (var item in itemsToDelete)
            {
                try
                {
                    if (Directory.Exists(item.Path))
                    {
                        Directory.Delete(item.Path, true);
                        TargetFolders.Remove(item);
                        successCount++;
                    }
                    else if (File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                        TargetFolders.Remove(item);
                        successCount++;
                    }
                }
                catch { failCount++; }
            }

            StatusMessage = $"청소 완료 (성공: {successCount}, 실패: {failCount})";
            MessageBox.Show($"청소 결과\n성공: {successCount}\n실패: {failCount}");
        }

        private static long GetDirectorySize(string path)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                .Sum(t => new FileInfo(t).Length);
            }
            catch { return 0; }
        }
    }

    // RelayCommand 구현체 (동일)
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter!);
        public event EventHandler? CanExecuteChanged;
    }
}