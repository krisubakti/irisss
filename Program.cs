using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using System.Security.Cryptography;

namespace Irys_MultiGame
{
    internal class Program
    {
        public static readonly object _lock = new();
        public static List<AccountInfo> AccountInfoList = [];
        public static int ScriptRunCount = 5;
        public static int RunMode = 1;
        public static decimal GameCost = 0.001m; // Biaya permainan dalam IRYS
        public static int GameType = 0; // 0: Spritetype, 1: Snake, 2: Keduanya, 3: HexShot, 4: Missile Command, 5: Asteroids
        public static bool HighScoreMode = false; // Mode khusus untuk score tinggi
        public static bool UltraHighScoreMode = false; // Mode khusus untuk score ultra tinggi

        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            LoadAccountsAndProxy();

            Console.Write("Silakan pilih game yang ingin dijalankan (0: Spritetype, 1: Snake, 2: Keduanya, 3: HexShot, 4: Missile Command, 5: Asteroids): ");
            var inputGameType = Console.ReadLine();
            if (int.TryParse(inputGameType, out int gameType) && (gameType >= 0 && gameType <= 5))
                GameType = gameType;

            Console.Write("Silakan masukkan berapa kali setiap akun akan mengeksekusi skrip: ");
            var inputCount = Console.ReadLine();
            if (int.TryParse(inputCount, out int count) && count > 0)
                ScriptRunCount = count;

            Console.Write("Silakan pilih mode operasi (1. Mode Aman 2. Mode Cepat): ");
            var inputMode = Console.ReadLine();
            if (int.TryParse(inputMode, out int mode) && (mode == 1 || mode == 2))
                RunMode = mode;

            Console.Write("Apakah Anda ingin mencapai score tinggi? (y/n): ");
            var highScoreInput = Console.ReadLine();
            if (highScoreInput?.ToLower() == "y" || highScoreInput?.ToLower() == "yes")
            {
                HighScoreMode = true;
                ShowMsg("Mode High Score diaktifkan! Bot akan mencoba mencapai score tinggi.", 1);

                Console.Write("Apakah Anda ingin mencapai score ultra tinggi? (y/n): ");
                var ultraHighScoreInput = Console.ReadLine();
                if (ultraHighScoreInput?.ToLower() == "y" || ultraHighScoreInput?.ToLower() == "yes")
                {
                    UltraHighScoreMode = true;
                    ShowMsg("Mode Ultra High Score diaktifkan! Bot akan mencoba mencapai score ultra tinggi.", 1);
                }
            }

            while (true)
            {
                foreach (var account in AccountInfoList)
                {
                    if (DateTime.Now >= account.NextExecutionTime)
                    {
                        try
                        {
                            // Jalankan game yang dipilih
                            if (GameType == 0 || GameType == 2)
                            {
                                if (!string.IsNullOrEmpty(account.Address))
                                    await RunSpritetypeGame(account);
                                else
                                    ShowMsg($"Alamat tidak tersedia untuk akun {account.Index}", 2);
                            }

                            if (GameType == 1 || GameType == 2)
                            {
                                if (!string.IsNullOrEmpty(account.PrivateKey))
                                    await RunSnakeGame(account);
                                else
                                    ShowMsg($"Private key tidak tersedia untuk akun {account.Index}", 2);
                            }

                            if (GameType == 3)
                            {
                                if (!string.IsNullOrEmpty(account.PrivateKey))
                                    await RunHexShotGame(account);
                                else
                                    ShowMsg($"Private key tidak tersedia untuk akun {account.Index}", 2);
                            }

                            if (GameType == 4)
                            {
                                if (!string.IsNullOrEmpty(account.PrivateKey))
                                    await RunMissileCommandGame(account);
                                else
                                    ShowMsg($"Private key tidak tersedia untuk akun {account.Index}", 2);
                            }

                            if (GameType == 5)
                            {
                                if (!string.IsNullOrEmpty(account.PrivateKey))
                                    await RunAsteroidsGame(account);
                                else
                                    ShowMsg($"Private key tidak tersedia untuk akun {account.Index}", 2);
                            }

                            account.NextExecutionTime = DateTime.Now.AddMinutes(1445);
                            account.FailTime = 0;
                        }
                        catch (Exception ex)
                        {
                            if (account.FailTime < 15)
                                account.FailTime += 1;
                            account.NextExecutionTime = DateTime.Now.AddSeconds(10);
                            ShowMsg($"Pengecualian eksekusi (ke-{account.FailTime}): {ex.Message}", 3);
                        }
                    }
                }
                Thread.Sleep(1000);
            }
        }

        // Game Spritetype (hanya memerlukan alamat)
        public static async Task RunSpritetypeGame(AccountInfo accountInfo)
        {
            ShowMsg($"Menjalankan game Spritetype untuk akun {accountInfo.Index} - {accountInfo.Address}", 0);

            for (int i = 0; i < ScriptRunCount; i++)
            {
                string result = await Spritetype(accountInfo);
                ShowMsg($"Pengiriman hasil Spritetype ke-{i + 1}: " + result, 1);

                if (!result.Contains("Successfully submitted to leaderboard!"))
                {
                    i = i - 1; // Ulangi jika gagal
                }

                // Jeda antar game
                if (RunMode == 2) // Mode Cepat
                {
                    Thread.Sleep(1000);
                }
                else // Mode Aman
                {
                    ShowMsg("Melanjutkan putaran berikutnya setelah 35 detik", 1);
                    Thread.Sleep(35000);
                }
            }
        }

        // Game Snake (memerlukan private key) - TIDAK DIUBAH
        public static async Task RunSnakeGame(AccountInfo accountInfo)
        {
            ShowMsg($"Menjalankan game Snake untuk akun {accountInfo.Index} - {accountInfo.Address}", 0);

            for (int i = 0; i < ScriptRunCount; i++)
            {
                try
                {
                    // Mulai game
                    GameStartResult startResult = await StartSnakeGame(accountInfo);
                    ShowMsg($"Memulai game Snake ke-{i + 1}: " + startResult.Message, 1);

                    // Jika berhasil memulai game, tunggu beberapa detik lalu kirim hasil
                    if (startResult.Success)
                    {
                        // Tunggu beberapa detik untuk mensimulasikan permainan
                        int gameDuration;
                        int score;
                        bool success = false;

                        if (UltraHighScoreMode)
                        {
                            // Untuk ultra high score (1000+), kita coba beberapa tingkatan score
                            int[] scoreAttempts = new[] { 1200, 1100, 1000, 950, 900 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 120 + (targetScore - 900) / 3; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode Ultra High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitSnakeGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🚀🚀🚀 LUAR BIASA! Anda telah mencapai score {targetScore}! 🚀🚀🚀", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 1000+.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(15000); // Jeda lebih lama untuk ultra high score
                                }
                            }

                            // Jika semua percobaan ultra high score gagal, coba high score biasa
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan ultra high score gagal, mencoba dengan high score biasa...", 2);

                                // Coba high score biasa
                                int[] normalHighScoreAttempts = new[] { 800, 700, 600, 500 };

                                foreach (int targetScore in normalHighScoreAttempts)
                                {
                                    gameDuration = 60 + (targetScore - 500) / 5; // Semakin tinggi score, semakin lama durasinya

                                    ShowMsg($"Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                    ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                                    Thread.Sleep(gameDuration * 1000);

                                    // Kirim hasil game
                                    string submitResult = await SubmitSnakeGameResult(accountInfo, startResult, targetScore, gameDuration);
                                    ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                    // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                    if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                    {
                                        ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore}!", 1);
                                        ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                        success = true;
                                        break;
                                    }
                                    else
                                    {
                                        // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                        ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                        Thread.Sleep(10000);
                                    }
                                }
                            }

                            // Jika semua percobaan gagal, gunakan score normal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(30, 60);
                                score = new Random().Next(300, 450);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitSnakeGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else if (HighScoreMode)
                        {
                            // Untuk high score, kita coba beberapa tingkatan score
                            int[] scoreAttempts = new[] { 800, 700, 600, 500 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 60 + (targetScore - 500) / 5; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitSnakeGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore}!", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(10000);
                                }
                            }

                            // Jika semua percobaan gagal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(30, 60);
                                score = new Random().Next(300, 450);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitSnakeGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else
                        {
                            // Mode normal
                            gameDuration = new Random().Next(10, 60);
                            score = new Random().Next(10, 500);

                            ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                            Thread.Sleep(gameDuration * 1000);

                            // Kirim hasil game
                            string submitResult = await SubmitSnakeGameResult(accountInfo, startResult, score, gameDuration);
                            ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {score}): " + submitResult, 1);
                        }
                    }
                    else
                    {
                        ShowMsg($"Gagal memulai game Snake, mencoba lagi...", 2);
                        i--; // Ulangi iterasi ini
                    }

                    // Jeda antar game
                    if (RunMode == 2) // Mode Cepat
                    {
                        Thread.Sleep(1000);
                    }
                    else // Mode Aman
                    {
                        ShowMsg("Melanjutkan putaran berikutnya setelah 35 detik", 1);
                        Thread.Sleep(35000);
                    }
                }
                catch (Exception ex)
                {
                    ShowMsg($"Error dalam iterasi Snake {i + 1}: {ex.Message}", 3);
                    i--; // Ulangi iterasi ini
                }
            }
        }

        // Game HexShot (memerlukan private key) - MODIFIKASI UNTUK SCORE 65.000+ dan TIMEOUT
        public static async Task RunHexShotGame(AccountInfo accountInfo)
        {
            ShowMsg($"Menjalankan game HexShot untuk akun {accountInfo.Index} - {accountInfo.Address}", 0);

            for (int i = 0; i < ScriptRunCount; i++)
            {
                try
                {
                    // Mulai game
                    GameStartResult startResult = await StartHexShotGame(accountInfo);
                    ShowMsg($"Memulai game HexShot ke-{i + 1}: " + startResult.Message, 1);

                    // Jika berhasil memulai game, tunggu beberapa detik lalu kirim hasil
                    if (startResult.Success)
                    {
                        // Tunggu beberapa detik untuk mensimulasikan permainan
                        int gameDuration;
                        int score;
                        bool success = false;

                        if (UltraHighScoreMode)
                        {
                            // Untuk ultra high score (100.000+), kita coba beberapa tingkatan score
                            int[] scoreAttempts = new[] { 120000, 110000, 100000, 90000, 80000 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 180 + (targetScore - 80000) / 300; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode Ultra High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan HexShot...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitHexShotGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game HexShot ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🚀🚀🚀 LUAR BIASA! Anda telah mencapai score {targetScore} di HexShot! 🚀🚀🚀", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 100.000+.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(15000); // Jeda lebih lama untuk ultra high score
                                }
                            }

                            // Jika semua percobaan ultra high score gagal, coba high score biasa
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan ultra high score gagal, mencoba dengan high score biasa...", 2);

                                // Coba high score biasa (65.000+)
                                int[] normalHighScoreAttempts = new[] { 80000, 75000, 70000, 65000 };

                                foreach (int targetScore in normalHighScoreAttempts)
                                {
                                    gameDuration = 150 + (targetScore - 65000) / 200; // Semakin tinggi score, semakin lama durasinya

                                    ShowMsg($"Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                    ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan HexShot...", 1);
                                    Thread.Sleep(gameDuration * 1000);

                                    // Kirim hasil game
                                    string submitResult = await SubmitHexShotGameResult(accountInfo, startResult, targetScore, gameDuration);
                                    ShowMsg($"Mengirim hasil game HexShot ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                    // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                    if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                    {
                                        ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore} di HexShot!", 1);
                                        ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                        success = true;
                                        break;
                                    }
                                    else
                                    {
                                        // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                        ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                        Thread.Sleep(10000);
                                    }
                                }
                            }

                            // Jika semua percobaan gagal, gunakan score normal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(60, 120);
                                score = new Random().Next(30000, 45000);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan HexShot...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitHexShotGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game HexShot ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else if (HighScoreMode)
                        {
                            // Untuk high score, kita coba beberapa tingkatan score (65.000+)
                            int[] scoreAttempts = new[] { 80000, 75000, 70000, 65000 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 150 + (targetScore - 65000) / 200; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan HexShot...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitHexShotGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game HexShot ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore} di HexShot!", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(10000);
                                }
                            }

                            // Jika semua percobaan gagal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(60, 120);
                                score = new Random().Next(30000, 45000);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan HexShot...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitHexShotGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game HexShot ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else
                        {
                            // Mode normal - Fokus pada score 65.000+
                            gameDuration = new Random().Next(120, 180);
                            score = new Random().Next(60000, 75000);

                            ShowMsg($"Mode Normal: Mencoba mencapai score 65.000+ dengan durasi {gameDuration} detik", 1);
                            ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan HexShot...", 1);
                            Thread.Sleep(gameDuration * 1000);

                            // Kirim hasil game
                            string submitResult = await SubmitHexShotGameResult(accountInfo, startResult, score, gameDuration);
                            ShowMsg($"Mengirim hasil game HexShot ke-{i + 1} (score: {score}): " + submitResult, 1);

                            // Jika berhasil dengan score 65.000+, beri notifikasi khusus
                            if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                            {
                                if (score >= 65000)
                                {
                                    ShowMsg($"🎯 TARGET TERCAPAI! Score {score} telah melewati 65.000! 🎯", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 65.000+.", 1);
                                }
                            }
                        }
                    }
                    else
                    {
                        ShowMsg($"Gagal memulai game HexShot, mencoba lagi...", 2);
                        i--; // Ulangi iterasi ini
                    }

                    // Jeda antar game
                    if (RunMode == 2) // Mode Cepat
                    {
                        Thread.Sleep(1000);
                    }
                    else // Mode Aman
                    {
                        ShowMsg("Melanjutkan putaran berikutnya setelah 35 detik", 1);
                        Thread.Sleep(35000);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    ShowMsg($"Request timeout saat memulai game HexShot ke-{i + 1}: {ex.Message}", 2);
                    ShowMsg("Mencoba lagi dalam 30 detik...", 1);
                    Thread.Sleep(30000);
                    i--; // Ulangi iterasi ini
                    continue;
                }
                catch (Exception ex)
                {
                    ShowMsg($"Error dalam iterasi HexShot {i + 1}: {ex.Message}", 3);
                    i--; // Ulangi iterasi ini
                }
            }
        }

        // Game Missile Command (memerlukan private key) - TARGET SCORE 1.600.000+
        public static async Task RunMissileCommandGame(AccountInfo accountInfo)
        {
            ShowMsg($"Menjalankan game Missile Command untuk akun {accountInfo.Index} - {accountInfo.Address}", 0);

            for (int i = 0; i < ScriptRunCount; i++)
            {
                try
                {
                    // Mulai game
                    GameStartResult startResult = await StartMissileCommandGame(accountInfo);
                    ShowMsg($"Memulai game Missile Command ke-{i + 1}: " + startResult.Message, 1);

                    // Jika berhasil memulai game, tunggu beberapa detik lalu kirim hasil
                    if (startResult.Success)
                    {
                        // Tunggu beberapa detik untuk mensimulasikan permainan
                        int gameDuration;
                        int score;
                        bool success = false;

                        if (UltraHighScoreMode)
                        {
                            // Untuk ultra high score (2.000.000+), kita coba beberapa tingkatan score
                            int[] scoreAttempts = new[] { 2500000, 2200000, 2000000, 1800000, 1600000 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 300 + (targetScore - 1600000) / 2000; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode Ultra High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Missile Command...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitMissileCommandGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game Missile Command ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🚀🚀🚀 LUAR BIASA! Anda telah mencapai score {targetScore} di Missile Command! 🚀🚀🚀", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 2.000.000+.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(20000); // Jeda lebih lama untuk ultra high score
                                }
                            }

                            // Jika semua percobaan ultra high score gagal, coba high score biasa
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan ultra high score gagal, mencoba dengan high score biasa...", 2);

                                // Coba high score biasa (1.600.000+)
                                int[] normalHighScoreAttempts = new[] { 1900000, 1800000, 1700000, 1600000 };

                                foreach (int targetScore in normalHighScoreAttempts)
                                {
                                    gameDuration = 250 + (targetScore - 1600000) / 1000; // Semakin tinggi score, semakin lama durasinya

                                    ShowMsg($"Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                    ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Missile Command...", 1);
                                    Thread.Sleep(gameDuration * 1000);

                                    // Kirim hasil game
                                    string submitResult = await SubmitMissileCommandGameResult(accountInfo, startResult, targetScore, gameDuration);
                                    ShowMsg($"Mengirim hasil game Missile Command ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                    // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                    if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                    {
                                        ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore} di Missile Command!", 1);
                                        ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                        success = true;
                                        break;
                                    }
                                    else
                                    {
                                        // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                        ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                        Thread.Sleep(15000);
                                    }
                                }
                            }

                            // Jika semua percobaan gagal, gunakan score normal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(120, 240);
                                score = new Random().Next(1000000, 1400000);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Missile Command...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitMissileCommandGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game Missile Command ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else if (HighScoreMode)
                        {
                            // Untuk high score, kita coba beberapa tingkatan score (1.600.000+)
                            int[] scoreAttempts = new[] { 1900000, 1800000, 1700000, 1600000 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 250 + (targetScore - 1600000) / 1000; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Missile Command...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitMissileCommandGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game Missile Command ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore} di Missile Command!", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(15000);
                                }
                            }

                            // Jika semua percobaan gagal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(120, 240);
                                score = new Random().Next(1000000, 1400000);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Missile Command...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitMissileCommandGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game Missile Command ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else
                        {
                            // Mode normal - Fokus pada score 1.600.000+
                            gameDuration = new Random().Next(240, 360);
                            score = new Random().Next(1500000, 1750000);

                            ShowMsg($"Mode Normal: Mencoba mencapai score 1.600.000+ dengan durasi {gameDuration} detik", 1);
                            ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Missile Command...", 1);
                            Thread.Sleep(gameDuration * 1000);

                            // Kirim hasil game
                            string submitResult = await SubmitMissileCommandGameResult(accountInfo, startResult, score, gameDuration);
                            ShowMsg($"Mengirim hasil game Missile Command ke-{i + 1} (score: {score}): " + submitResult, 1);

                            // Jika berhasil dengan score 1.600.000+, beri notifikasi khusus
                            if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                            {
                                if (score >= 1600000)
                                {
                                    ShowMsg($"🎯 TARGET TERCAPAI! Score {score} telah melewati 1.600.000! 🎯", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 1.600.000+.", 1);
                                }
                            }
                        }
                    }
                    else
                    {
                        ShowMsg($"Gagal memulai game Missile Command, mencoba lagi...", 2);
                        i--; // Ulangi iterasi ini
                    }

                    // Jeda antar game
                    if (RunMode == 2) // Mode Cepat
                    {
                        Thread.Sleep(1000);
                    }
                    else // Mode Aman
                    {
                        ShowMsg("Melanjutkan putaran berikutnya setelah 35 detik", 1);
                        Thread.Sleep(35000);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    ShowMsg($"Request timeout saat memulai game Missile Command ke-{i + 1}: {ex.Message}", 2);
                    ShowMsg("Mencoba lagi dalam 30 detik...", 1);
                    Thread.Sleep(30000);
                    i--; // Ulangi iterasi ini
                    continue;
                }
                catch (Exception ex)
                {
                    ShowMsg($"Error dalam iterasi Missile Command {i + 1}: {ex.Message}", 3);
                    i--; // Ulangi iterasi ini
                }
            }
        }

        // Game Asteroids (memerlukan private key) - TARGET SCORE 500.000+
        public static async Task RunAsteroidsGame(AccountInfo accountInfo)
        {
            ShowMsg($"Menjalankan game Asteroids untuk akun {accountInfo.Index} - {accountInfo.Address}", 0);

            for (int i = 0; i < ScriptRunCount; i++)
            {
                try
                {
                    // Mulai game
                    GameStartResult startResult = await StartAsteroidsGame(accountInfo);
                    ShowMsg($"Memulai game Asteroids ke-{i + 1}: " + startResult.Message, 1);

                    // Jika berhasil memulai game, tunggu beberapa detik lalu kirim hasil
                    if (startResult.Success)
                    {
                        // Tunggu beberapa detik untuk mensimulasikan permainan
                        int gameDuration;
                        int score;
                        bool success = false;

                        if (UltraHighScoreMode)
                        {
                            // Untuk ultra high score (700.000+), kita coba beberapa tingkatan score
                            int[] scoreAttempts = new[] { 800000, 750000, 700000, 650000, 600000 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 210 + (targetScore - 600000) / 1000; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode Ultra High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Asteroids...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitAsteroidsGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game Asteroids ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🚀🚀🚀 LUAR BIASA! Anda telah mencapai score {targetScore} di Asteroids! 🚀🚀🚀", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 700.000+.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(15000); // Jeda lebih lama untuk ultra high score
                                }
                            }

                            // Jika semua percobaan ultra high score gagal, coba high score biasa
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan ultra high score gagal, mencoba dengan high score biasa...", 2);

                                // Coba high score biasa (500.000+)
                                int[] normalHighScoreAttempts = new[] { 600000, 580000, 550000, 500000 };

                                foreach (int targetScore in normalHighScoreAttempts)
                                {
                                    gameDuration = 180 + (targetScore - 500000) / 800; // Semakin tinggi score, semakin lama durasinya

                                    ShowMsg($"Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                    ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Asteroids...", 1);
                                    Thread.Sleep(gameDuration * 1000);

                                    // Kirim hasil game
                                    string submitResult = await SubmitAsteroidsGameResult(accountInfo, startResult, targetScore, gameDuration);
                                    ShowMsg($"Mengirim hasil game Asteroids ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                    // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                    if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                    {
                                        ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore} di Asteroids!", 1);
                                        ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                        success = true;
                                        break;
                                    }
                                    else
                                    {
                                        // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                        ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                        Thread.Sleep(10000);
                                    }
                                }
                            }

                            // Jika semua percobaan gagal, gunakan score normal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(120, 180);
                                score = new Random().Next(350000, 450000);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Asteroids...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitAsteroidsGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game Asteroids ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else if (HighScoreMode)
                        {
                            // Untuk high score, kita coba beberapa tingkatan score (500.000+)
                            int[] scoreAttempts = new[] { 600000, 580000, 550000, 500000 };

                            foreach (int targetScore in scoreAttempts)
                            {
                                // Durasi permainan disesuaikan dengan target score
                                gameDuration = 180 + (targetScore - 500000) / 800; // Semakin tinggi score, semakin lama durasinya

                                ShowMsg($"Mode High Score: Mencoba dengan score {targetScore} dan durasi {gameDuration} detik", 1);
                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Asteroids...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitAsteroidsGameResult(accountInfo, startResult, targetScore, gameDuration);
                                ShowMsg($"Mengirim hasil game Asteroids ke-{i + 1} (score: {targetScore}): " + submitResult, 1);

                                // Jika berhasil, tampilkan notifikasi dan lanjutkan ke permainan berikutnya
                                if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                                {
                                    ShowMsg($"🎉 SELAMAT! Anda telah mencapai score {targetScore} di Asteroids!", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti.", 1);
                                    success = true;
                                    break;
                                }
                                else
                                {
                                    // Jika gagal, tunggu beberapa detik sebelum mencoba score yang lebih rendah
                                    ShowMsg($"Gagal dengan score {targetScore}, mencoba dengan score lebih rendah...", 2);
                                    Thread.Sleep(10000);
                                }
                            }

                            // Jika semua percobaan gagal
                            if (!success)
                            {
                                ShowMsg($"Semua percobaan high score gagal, menggunakan score normal...", 2);
                                gameDuration = new Random().Next(120, 180);
                                score = new Random().Next(350000, 450000);

                                ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Asteroids...", 1);
                                Thread.Sleep(gameDuration * 1000);

                                // Kirim hasil game
                                string submitResult = await SubmitAsteroidsGameResult(accountInfo, startResult, score, gameDuration);
                                ShowMsg($"Mengirim hasil game Asteroids ke-{i + 1} (score: {score}): " + submitResult, 1);
                            }
                        }
                        else
                        {
                            // Mode normal - Fokus pada score 500.000+
                            gameDuration = new Random().Next(150, 210);
                            score = new Random().Next(480000, 550000);

                            ShowMsg($"Mode Normal: Mencoba mencapai score 500.000+ dengan durasi {gameDuration} detik", 1);
                            ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Asteroids...", 1);
                            Thread.Sleep(gameDuration * 1000);

                            // Kirim hasil game
                            string submitResult = await SubmitAsteroidsGameResult(accountInfo, startResult, score, gameDuration);
                            ShowMsg($"Mengirim hasil game Asteroids ke-{i + 1} (score: {score}): " + submitResult, 1);

                            // Jika berhasil dengan score 500.000+, beri notifikasi khusus
                            if (submitResult.Contains("successfully") || submitResult.Contains("completed"))
                            {
                                if (score >= 500000)
                                {
                                    ShowMsg($"🎯 TARGET TERCAPAI! Score {score} telah melewati 500.000! 🎯", 1);
                                    ShowMsg($"Anda bisa screenshot hasil ini sebagai bukti pencapaian score 500.000+.", 1);
                                }
                            }
                        }
                    }
                    else
                    {
                        ShowMsg($"Gagal memulai game Asteroids, mencoba lagi...", 2);
                        i--; // Ulangi iterasi ini
                    }

                    // Jeda antar game
                    if (RunMode == 2) // Mode Cepat
                    {
                        Thread.Sleep(1000);
                    }
                    else // Mode Aman
                    {
                        ShowMsg("Melanjutkan putaran berikutnya setelah 35 detik", 1);
                        Thread.Sleep(35000);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    ShowMsg($"Request timeout saat memulai game Asteroids ke-{i + 1}: {ex.Message}", 2);
                    ShowMsg("Mencoba lagi dalam 30 detik...", 1);
                    Thread.Sleep(30000);
                    i--; // Ulangi iterasi ini
                    continue;
                }
                catch (Exception ex)
                {
                    ShowMsg($"Error dalam iterasi Asteroids {i + 1}: {ex.Message}", 3);
                    i--; // Ulangi iterasi ini
                }
            }
        }

        // Fungsi untuk game Spritetype (dari script asli)
        public static async Task<string> Spritetype(AccountInfo accountInfo)
        {
            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy,
                    UseProxy = true
                };
            }
            HttpClient client = new(httpClientHandler);
            HttpRequestMessage request = new(HttpMethod.Post, "https://spritetype.irys.xyz/api/submit-result");
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "zh-CN,zh;q=0.9,zh-TW;q=0.8,ja;q=0.7,en;q=0.6");
            request.Headers.Add("origin", "https://spritetype.irys.xyz");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://spritetype.irys.xyz/");
            request.Headers.Add("sec-ch-ua", "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("user-agent", accountInfo.UserAgent);

            var rand = new Random();
            int wpm = rand.Next(70, 81); // WPM 70 ~ 80
            int time = 15; // Durasi tetap
            int totalChars = wpm * 5 * time / 60; // Total karakter
            int incorrectChars = rand.Next(0, Math.Max(1, totalChars / 20)); // Karakter salah, maks 5%
            int correctChars = totalChars - incorrectChars;
            int accuracy = totalChars == 0 ? 100 : (int)Math.Round(100.0 * correctChars / totalChars);
            int[] progressData = [];

            string antiCheatHash = ComputeAntiCheatHash(accountInfo.Address, wpm, accuracy, time, correctChars, incorrectChars);

            var payload = new
            {
                walletAddress = accountInfo.Address,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                gameStats = new
                {
                    accuracy,
                    correctChars,
                    incorrectChars,
                    progressData,
                    time,
                    wpm
                },
                antiCheatHash
            };

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                 response = await client.SendAsync(request);
            }
            catch(Exception ex)
            {
                ShowMsg($"Pengiriman gagal: " + ex.Message, 3);
                return "Pengiriman gagal";
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait"))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return "Pengiriman gagal karena batasan antarmuka";
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return "Pengiriman gagal: " + responseBody;
            }

            var json = System.Text.Json.JsonDocument.Parse(responseBody);
            json.RootElement.TryGetProperty("message", out var nonceElement);
            string? ret = nonceElement.GetString();
            if (!string.IsNullOrEmpty(ret))
            {
                return ret;
            }
            throw new Exception("Spritetype gagal");
        }

        // Fungsi untuk memulai game Snake - TIDAK DIUBAH
        public static async Task<GameStartResult> StartSnakeGame(AccountInfo accountInfo)
        {
            string sessionId = $"game_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{GenerateRandomString(8)}";

            // Gunakan timestamp yang sama untuk pesan dan payload
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan dengan benar (tanpa spasi ekstra)
            string messageToSign = $"I authorize payment of {GameCost} IRYS to play a game on Irys Arcade.\n\nPlayer: {accountInfo.Address}\nAmount: {GameCost} IRYS\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and authorize the payment.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy,
                    UseProxy = true
                };
            }

            using HttpClient client = new(httpClientHandler);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/start");

            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.5");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/snake");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            // PERUBAHAN: Coba kirim gameCost sebagai decimal, bukan string
            var payload = new
            {
                playerAddress = accountInfo.Address,
                gameCost = GameCost, // Kembalikan ke decimal
                signature,
                message = messageToSign,
                timestamp,
                sessionId,
                gameType = "snake"
            };

            // Tampilkan payload untuk debugging
            string payloadJson = JsonConvert.SerializeObject(payload, Formatting.Indented);
            ShowMsg($"Payload yang dikirim: {payloadJson}", 1);

            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman gagal: " + ex.Message, 3);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal karena batasan antarmuka" };
            }

            // Handle invalid game cost
            if (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Invalid game cost"))
            {
                ShowMsg($"Format game cost tidak valid, mencoba dengan format yang berbeda...", 2);

                // Coba dengan format string
                var payloadString = new
                {
                    playerAddress = accountInfo.Address,
                    gameCost = GameCost.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    signature,
                    message = messageToSign,
                    timestamp,
                    sessionId,
                    gameType = "snake"
                };

                string payloadStringJson = JsonConvert.SerializeObject(payloadString, Formatting.Indented);
                ShowMsg($"Mencoba dengan payload string: {payloadStringJson}", 1);

                request.Content = new StringContent(payloadStringJson);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                try
                {
                    response = await client.SendAsync(request);
                    responseBody = await response.Content.ReadAsStringAsync();
                    ShowMsg($"Response dengan format string: {response.StatusCode} - {responseBody}", 1);
                }
                catch (Exception ex)
                {
                    ShowMsg($"Pengiriman dengan format string gagal: " + ex.Message, 3);
                    return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
                }
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return new GameStartResult { Success = false, Message = "Server mengalami kesalahan internal" };
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal: " + responseBody };
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("success", out var successElement) &&
                    successElement.GetBoolean())
                {
                    string sessionIdFromResponse = "";
                    string sessionRecordUrl = "";
                    string transactionHash = "";
                    decimal gameCostDeducted = 0;
                    int blockNumber = 0;

                    if (json.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        if (dataElement.TryGetProperty("sessionId", out var sessionIdElement))
                        {
                            sessionIdFromResponse = sessionIdElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("sessionRecordUrl", out var sessionRecordUrlElement))
                        {
                            sessionRecordUrl = sessionRecordUrlElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("transactionHash", out var transactionHashElement))
                        {
                            transactionHash = transactionHashElement.GetString() ?? "";
                        }

                        // Handle both string and decimal types for gameCostDeducted
                        if (dataElement.TryGetProperty("gameCostDeducted", out var gameCostDeductedElement))
                        {
                            if (gameCostDeductedElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                gameCostDeducted = decimal.Parse(gameCostDeductedElement.GetString() ?? "0");
                            }
                            else
                            {
                                gameCostDeducted = gameCostDeductedElement.GetDecimal();
                            }
                        }

                        if (dataElement.TryGetProperty("blockNumber", out var blockNumberElement))
                        {
                            blockNumber = blockNumberElement.GetInt32();
                        }
                    }

                    string responseMessage = "Game berhasil dimulai";
                    if (json.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        responseMessage = messageElement.GetString() ?? "Game berhasil dimulai";
                    }

                    return new GameStartResult
                    {
                        Success = true,
                        Message = responseMessage,
                        SessionId = string.IsNullOrEmpty(sessionIdFromResponse) ? sessionId : sessionIdFromResponse,
                        SessionRecordUrl = sessionRecordUrl,
                        TransactionHash = transactionHash,
                        GameCostDeducted = gameCostDeducted,
                        BlockNumber = blockNumber
                    };
                }

                string errorMessage = "Game gagal dimulai";
                if (json.RootElement.TryGetProperty("message", out var errorMessageElement))
                {
                    errorMessage = errorMessageElement.GetString() ?? "Game gagal dimulai";
                }

                return new GameStartResult { Success = false, Message = errorMessage };
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return new GameStartResult { Success = false, Message = "Error parsing response" };
            }
        }

        // Fungsi untuk mengirim hasil game Snake - TIDAK DIUBAH
        public static async Task<string> SubmitSnakeGameResult(AccountInfo accountInfo, GameStartResult startResult, int score, int gameDuration)
        {
            // Generate timestamp untuk submit hasil
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan sesuai dengan format yang benar
            string messageToSign = $"I completed a snake game on Irys Arcade.\n    \nPlayer: {accountInfo.Address}\nGame: snake\nScore: {score}\nSession: {startResult.SessionId}\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and completed this game.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            // Format payload yang benar berdasarkan data manual
            var payload = new
            {
                playerAddress = accountInfo.Address,
                sessionId = startResult.SessionId,
                gameType = "snake",
                score = score, // Kirim score langsung, bukan di dalam gameStats
                timestamp,
                signature,
                message = messageToSign
            };

            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy,
                    UseProxy = true
                };
            }

            using HttpClient client = new(httpClientHandler);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/complete");

            // Tambahkan headers sesuai dengan yang diberikan
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.5");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/snake");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            ShowMsg($"Mengirim hasil game dengan payload: {payloadJson}", 1);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman hasil gagal: " + ex.Message, 3);
                return "Pengiriman hasil gagal";
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return "Pengiriman hasil gagal karena batasan antarmuka";
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal saat mengirim hasil, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return "Server mengalami kesalahan internal";
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return "Pengiriman hasil gagal: " + responseBody;
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? "Hasil berhasil dikirim";
                }
                if (json.RootElement.TryGetProperty("status", out var statusElement))
                {
                    return statusElement.GetString() ?? "Hasil berhasil dikirim";
                }
                return "Hasil berhasil dikirim";
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return "Error parsing response";
            }
        }

        // Fungsi untuk memulai game HexShot - MODIFIKASI TIMEOUT
        public static async Task<GameStartResult> StartHexShotGame(AccountInfo accountInfo)
        {
            string sessionId = $"game_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{GenerateRandomString(8)}";

            // Gunakan timestamp yang sama untuk pesan dan payload
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan dengan benar (tanpa spasi ekstra)
            string messageToSign = $"I authorize payment of {GameCost} IRYS to play a game on Irys Arcade.\n\nPlayer: {accountInfo.Address}\nAmount: {GameCost} IRYS\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and authorize the payment.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            using HttpClient client = CreateHttpClient(accountInfo);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/start");

            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.5");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/hexshot");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payload = new
            {
                playerAddress = accountInfo.Address,
                gameCost = GameCost,
                signature,
                message = messageToSign,
                timestamp,
                sessionId,
                gameType = "hex-shooter"
            };

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman gagal: " + ex.Message, 3);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal karena batasan antarmuka" };
            }

            // Handle invalid game cost
            if (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Invalid game cost"))
            {
                ShowMsg($"Format game cost tidak valid, mencoba dengan format yang berbeda...", 2);

                // Coba dengan format string
                var payloadString = new
                {
                    playerAddress = accountInfo.Address,
                    gameCost = GameCost.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    signature,
                    message = messageToSign,
                    timestamp,
                    sessionId,
                    gameType = "hex-shooter"
                };

                string payloadStringJson = JsonConvert.SerializeObject(payloadString);
                ShowMsg($"Mencoba dengan payload string: {payloadStringJson}", 1);

                request.Content = new StringContent(payloadStringJson);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                try
                {
                    response = await client.SendAsync(request);
                    responseBody = await response.Content.ReadAsStringAsync();
                    ShowMsg($"Response dengan format string: {response.StatusCode} - {responseBody}", 1);
                }
                catch (Exception ex)
                {
                    ShowMsg($"Pengiriman dengan format string gagal: " + ex.Message, 3);
                    return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
                }
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return new GameStartResult { Success = false, Message = "Server mengalami kesalahan internal" };
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal: " + responseBody };
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("success", out var successElement) &&
                    successElement.GetBoolean())
                {
                    string sessionIdFromResponse = "";
                    string sessionRecordUrl = "";
                    string transactionHash = "";
                    decimal gameCostDeducted = 0;
                    int blockNumber = 0;

                    if (json.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        if (dataElement.TryGetProperty("sessionId", out var sessionIdElement))
                        {
                            sessionIdFromResponse = sessionIdElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("sessionRecordUrl", out var sessionRecordUrlElement))
                        {
                            sessionRecordUrl = sessionRecordUrlElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("transactionHash", out var transactionHashElement))
                        {
                            transactionHash = transactionHashElement.GetString() ?? "";
                        }

                        // Handle both string and decimal types for gameCostDeducted
                        if (dataElement.TryGetProperty("gameCostDeducted", out var gameCostDeductedElement))
                        {
                            if (gameCostDeductedElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                gameCostDeducted = decimal.Parse(gameCostDeductedElement.GetString() ?? "0");
                            }
                            else
                            {
                                gameCostDeducted = gameCostDeductedElement.GetDecimal();
                            }
                        }

                        if (dataElement.TryGetProperty("blockNumber", out var blockNumberElement))
                        {
                            blockNumber = blockNumberElement.GetInt32();
                        }
                    }

                    string responseMessage = "Game berhasil dimulai";
                    if (json.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        responseMessage = messageElement.GetString() ?? "Game berhasil dimulai";
                    }

                    return new GameStartResult
                    {
                        Success = true,
                        Message = responseMessage,
                        SessionId = string.IsNullOrEmpty(sessionIdFromResponse) ? sessionId : sessionIdFromResponse,
                        SessionRecordUrl = sessionRecordUrl,
                        TransactionHash = transactionHash,
                        GameCostDeducted = gameCostDeducted,
                        BlockNumber = blockNumber
                    };
                }

                string errorMessage = "Game gagal dimulai";
                if (json.RootElement.TryGetProperty("message", out var errorMessageElement))
                {
                    errorMessage = errorMessageElement.GetString() ?? "Game gagal dimulai";
                }

                return new GameStartResult { Success = false, Message = errorMessage };
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return new GameStartResult { Success = false, Message = "Error parsing response" };
            }
        }

        // Fungsi untuk mengirim hasil game HexShot - MODIFIKASI TIMEOUT
        public static async Task<string> SubmitHexShotGameResult(AccountInfo accountInfo, GameStartResult startResult, int score, int gameDuration)
        {
            // Generate timestamp untuk submit hasil
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan sesuai dengan format yang benar
            string messageToSign = $"I completed a hex-shooter game on Irys Arcade.\n    \nPlayer: {accountInfo.Address}\nGame: hex-shooter\nScore: {score}\nSession: {startResult.SessionId}\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and completed this game.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            // Format payload yang benar berdasarkan data manual
            var payload = new
            {
                playerAddress = accountInfo.Address,
                sessionId = startResult.SessionId,
                gameType = "hex-shooter",
                score = score, // Kirim score langsung, bukan di dalam gameStats
                timestamp,
                signature,
                message = messageToSign
            };

            using HttpClient client = CreateHttpClient(accountInfo);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/complete");

            // Tambahkan headers sesuai dengan yang diberikan
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.5");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/hexshot");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            ShowMsg($"Mengirim hasil game dengan payload: {payloadJson}", 1);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman hasil gagal: " + ex.Message, 3);
                return "Pengiriman hasil gagal";
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return "Pengiriman hasil gagal karena batasan antarmuka";
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal saat mengirim hasil, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return "Server mengalami kesalahan internal";
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return "Pengiriman hasil gagal: " + responseBody;
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? "Hasil berhasil dikirim";
                }
                if (json.RootElement.TryGetProperty("status", out var statusElement))
                {
                    return statusElement.GetString() ?? "Hasil berhasil dikirim";
                }
                return "Hasil berhasil dikirim";
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return "Error parsing response";
            }
        }

        // Fungsi untuk memulai game Missile Command - TARGET SCORE 1.600.000+
        public static async Task<GameStartResult> StartMissileCommandGame(AccountInfo accountInfo)
        {
            string sessionId = $"game_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{GenerateRandomString(8)}";

            // Gunakan timestamp yang sama untuk pesan dan payload
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan dengan benar (tanpa spasi ekstra)
            string messageToSign = $"I authorize payment of {GameCost} IRYS to play a game on Irys Arcade.\n\nPlayer: {accountInfo.Address}\nAmount: {GameCost} IRYS\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and authorize the payment.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            using HttpClient client = CreateHttpClient(accountInfo);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/start");

            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.5");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/missile");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payload = new
            {
                playerAddress = accountInfo.Address,
                gameCost = GameCost,
                signature,
                message = messageToSign,
                timestamp,
                sessionId,
                gameType = "missile-command"
            };

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman gagal: " + ex.Message, 3);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal karena batasan antarmuka" };
            }

            // Handle invalid game cost
            if (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Invalid game cost"))
            {
                ShowMsg($"Format game cost tidak valid, mencoba dengan format yang berbeda...", 2);

                // Coba dengan format string
                var payloadString = new
                {
                    playerAddress = accountInfo.Address,
                    gameCost = GameCost.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    signature,
                    message = messageToSign,
                    timestamp,
                    sessionId,
                    gameType = "missile-command"
                };

                string payloadStringJson = JsonConvert.SerializeObject(payloadString);
                ShowMsg($"Mencoba dengan payload string: {payloadStringJson}", 1);

                request.Content = new StringContent(payloadStringJson);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                try
                {
                    response = await client.SendAsync(request);
                    responseBody = await response.Content.ReadAsStringAsync();
                    ShowMsg($"Response dengan format string: {response.StatusCode} - {responseBody}", 1);
                }
                catch (Exception ex)
                {
                    ShowMsg($"Pengiriman dengan format string gagal: " + ex.Message, 3);
                    return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
                }
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return new GameStartResult { Success = false, Message = "Server mengalami kesalahan internal" };
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal: " + responseBody };
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("success", out var successElement) &&
                    successElement.GetBoolean())
                {
                    string sessionIdFromResponse = "";
                    string sessionRecordUrl = "";
                    string transactionHash = "";
                    decimal gameCostDeducted = 0;
                    int blockNumber = 0;

                    if (json.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        if (dataElement.TryGetProperty("sessionId", out var sessionIdElement))
                        {
                            sessionIdFromResponse = sessionIdElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("sessionRecordUrl", out var sessionRecordUrlElement))
                        {
                            sessionRecordUrl = sessionRecordUrlElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("transactionHash", out var transactionHashElement))
                        {
                            transactionHash = transactionHashElement.GetString() ?? "";
                        }

                        // Handle both string and decimal types for gameCostDeducted
                        if (dataElement.TryGetProperty("gameCostDeducted", out var gameCostDeductedElement))
                        {
                            if (gameCostDeductedElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                gameCostDeducted = decimal.Parse(gameCostDeductedElement.GetString() ?? "0");
                            }
                            else
                            {
                                gameCostDeducted = gameCostDeductedElement.GetDecimal();
                            }
                        }

                        if (dataElement.TryGetProperty("blockNumber", out var blockNumberElement))
                        {
                            blockNumber = blockNumberElement.GetInt32();
                        }
                    }

                    string responseMessage = "Game berhasil dimulai";
                    if (json.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        responseMessage = messageElement.GetString() ?? "Game berhasil dimulai";
                    }

                    return new GameStartResult
                    {
                        Success = true,
                        Message = responseMessage,
                        SessionId = string.IsNullOrEmpty(sessionIdFromResponse) ? sessionId : sessionIdFromResponse,
                        SessionRecordUrl = sessionRecordUrl,
                        TransactionHash = transactionHash,
                        GameCostDeducted = gameCostDeducted,
                        BlockNumber = blockNumber
                    };
                }

                string errorMessage = "Game gagal dimulai";
                if (json.RootElement.TryGetProperty("message", out var errorMessageElement))
                {
                    errorMessage = errorMessageElement.GetString() ?? "Game gagal dimulai";
                }

                return new GameStartResult { Success = false, Message = errorMessage };
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return new GameStartResult { Success = false, Message = "Error parsing response" };
            }
        }

        // Fungsi untuk mengirim hasil game Missile Command - TARGET SCORE 1.600.000+
        public static async Task<string> SubmitMissileCommandGameResult(AccountInfo accountInfo, GameStartResult startResult, int score, int gameDuration)
        {
            // Generate timestamp untuk submit hasil
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan sesuai dengan format yang benar
            string messageToSign = $"I completed a missile-command game on Irys Arcade.\n    \nPlayer: {accountInfo.Address}\nGame: missile-command\nScore: {score}\nSession: {startResult.SessionId}\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and completed this game.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            // Format payload yang benar berdasarkan data manual
            var payload = new
            {
                playerAddress = accountInfo.Address,
                sessionId = startResult.SessionId,
                gameType = "missile-command",
                score = score, // Kirim score langsung, bukan di dalam gameStats
                timestamp,
                signature,
                message = messageToSign
            };

            using HttpClient client = CreateHttpClient(accountInfo);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/complete");

            // Tambahkan headers sesuai dengan yang diberikan
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.5");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/missile");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            ShowMsg($"Mengirim hasil game dengan payload: {payloadJson}", 1);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman hasil gagal: " + ex.Message, 3);
                return "Pengiriman hasil gagal";
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return "Pengiriman hasil gagal karena batasan antarmuka";
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal saat mengirim hasil, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return "Server mengalami kesalahan internal";
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return "Pengiriman hasil gagal: " + responseBody;
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? "Hasil berhasil dikirim";
                }
                if (json.RootElement.TryGetProperty("status", out var statusElement))
                {
                    return statusElement.GetString() ?? "Hasil berhasil dikirim";
                }
                return "Hasil berhasil dikirim";
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return "Error parsing response";
            }
        }

        // Fungsi untuk memulai game Asteroids - TARGET SCORE 500.000+
        public static async Task<GameStartResult> StartAsteroidsGame(AccountInfo accountInfo)
        {
            string sessionId = $"game_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{GenerateRandomString(8)}";

            // Gunakan timestamp yang sama untuk pesan dan payload
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan dengan benar (tanpa spasi ekstra)
            string messageToSign = $"I authorize payment of {GameCost} IRYS to play a game on Irys Arcade.\n\nPlayer: {accountInfo.Address}\nAmount: {GameCost} IRYS\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and authorize the payment.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            using HttpClient client = CreateHttpClient(accountInfo);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/start");

            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.8");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/asteroids");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payload = new
            {
                playerAddress = accountInfo.Address,
                gameCost = GameCost,
                signature,
                message = messageToSign,
                timestamp,
                sessionId,
                gameType = "asteroids"
            };

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman gagal: " + ex.Message, 3);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal karena batasan antarmuka" };
            }

            // Handle invalid game cost
            if (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Invalid game cost"))
            {
                ShowMsg($"Format game cost tidak valid, mencoba dengan format yang berbeda...", 2);

                // Coba dengan format string
                var payloadString = new
                {
                    playerAddress = accountInfo.Address,
                    gameCost = GameCost.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    signature,
                    message = messageToSign,
                    timestamp,
                    sessionId,
                    gameType = "asteroids"
                };

                string payloadStringJson = JsonConvert.SerializeObject(payloadString);
                ShowMsg($"Mencoba dengan payload string: {payloadStringJson}", 1);

                request.Content = new StringContent(payloadStringJson);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                try
                {
                    response = await client.SendAsync(request);
                    responseBody = await response.Content.ReadAsStringAsync();
                    ShowMsg($"Response dengan format string: {response.StatusCode} - {responseBody}", 1);
                }
                catch (Exception ex)
                {
                    ShowMsg($"Pengiriman dengan format string gagal: " + ex.Message, 3);
                    return new GameStartResult { Success = false, Message = "Pengiriman gagal" };
                }
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return new GameStartResult { Success = false, Message = "Server mengalami kesalahan internal" };
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return new GameStartResult { Success = false, Message = "Pengiriman gagal: " + responseBody };
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("success", out var successElement) &&
                    successElement.GetBoolean())
                {
                    string sessionIdFromResponse = "";
                    string sessionRecordUrl = "";
                    string transactionHash = "";
                    decimal gameCostDeducted = 0;
                    int blockNumber = 0;

                    if (json.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        if (dataElement.TryGetProperty("sessionId", out var sessionIdElement))
                        {
                            sessionIdFromResponse = sessionIdElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("sessionRecordUrl", out var sessionRecordUrlElement))
                        {
                            sessionRecordUrl = sessionRecordUrlElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("transactionHash", out var transactionHashElement))
                        {
                            transactionHash = transactionHashElement.GetString() ?? "";
                        }

                        // Handle both string and decimal types for gameCostDeducted
                        if (dataElement.TryGetProperty("gameCostDeducted", out var gameCostDeductedElement))
                        {
                            if (gameCostDeductedElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                gameCostDeducted = decimal.Parse(gameCostDeductedElement.GetString() ?? "0");
                            }
                            else
                            {
                                gameCostDeducted = gameCostDeductedElement.GetDecimal();
                            }
                        }

                        if (dataElement.TryGetProperty("blockNumber", out var blockNumberElement))
                        {
                            blockNumber = blockNumberElement.GetInt32();
                        }
                    }

                    string responseMessage = "Game berhasil dimulai";
                    if (json.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        responseMessage = messageElement.GetString() ?? "Game berhasil dimulai";
                    }

                    return new GameStartResult
                    {
                        Success = true,
                        Message = responseMessage,
                        SessionId = string.IsNullOrEmpty(sessionIdFromResponse) ? sessionId : sessionIdFromResponse,
                        SessionRecordUrl = sessionRecordUrl,
                        TransactionHash = transactionHash,
                        GameCostDeducted = gameCostDeducted,
                        BlockNumber = blockNumber
                    };
                }

                string errorMessage = "Game gagal dimulai";
                if (json.RootElement.TryGetProperty("message", out var errorMessageElement))
                {
                    errorMessage = errorMessageElement.GetString() ?? "Game gagal dimulai";
                }

                return new GameStartResult { Success = false, Message = errorMessage };
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return new GameStartResult { Success = false, Message = "Error parsing response" };
            }
        }

        // Fungsi untuk mengirim hasil game Asteroids - TARGET SCORE 500.000+
        public static async Task<string> SubmitAsteroidsGameResult(AccountInfo accountInfo, GameStartResult startResult, int score, int gameDuration)
        {
            // Generate timestamp untuk submit hasil
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Format pesan sesuai dengan format yang benar
            string messageToSign = $"I completed a asteroids game on Irys Arcade.\n    \nPlayer: {accountInfo.Address}\nGame: asteroids\nScore: {score}\nSession: {startResult.SessionId}\nTimestamp: {timestamp}\n\nThis signature confirms I own this wallet and completed this game.";

            string signature = SignMessage(accountInfo.PrivateKey, messageToSign);

            // Format payload yang benar berdasarkan data manual
            var payload = new
            {
                playerAddress = accountInfo.Address,
                sessionId = startResult.SessionId,
                gameType = "asteroids",
                score = score, // Kirim score langsung, bukan di dalam gameStats
                timestamp,
                signature,
                message = messageToSign
            };

            using HttpClient client = CreateHttpClient(accountInfo);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/complete");

            // Tambahkan headers sesuai dengan yang diberikan
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.8");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://play.irys.xyz/asteroids");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");

            var payloadJson = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(payloadJson);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            ShowMsg($"Mengirim hasil game dengan payload: {payloadJson}", 1);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex)
            {
                ShowMsg($"Pengiriman hasil gagal: " + ex.Message, 3);
                return "Pengiriman hasil gagal";
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            ShowMsg($"Response dari server: {response.StatusCode} - {responseBody}", 1);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.BadRequest && responseBody.Contains("Please wait")))
            {
                int waitSeconds = 30;
                var match = System.Text.RegularExpressions.Regex.Match(responseBody, @"Please wait (\d+) seconds");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int sec))
                {
                    waitSeconds = sec;
                }
                ShowMsg($"Antarmuka dibatasi, coba lagi setelah {waitSeconds} detik...", 2);
                Thread.Sleep(waitSeconds * 1000);
                return "Pengiriman hasil gagal karena batasan antarmuka";
            }

            // Handle internal server error
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                ShowMsg($"Server mengalami kesalahan internal saat mengirim hasil, mencoba lagi setelah 60 detik...", 2);
                Thread.Sleep(60000); // Tunggu lebih lama untuk internal server error
                return "Server mengalami kesalahan internal";
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                ShowMsg($"Informasi pengecualian: " + ex.Message, 2);
                return "Pengiriman hasil gagal: " + responseBody;
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? "Hasil berhasil dikirim";
                }
                if (json.RootElement.TryGetProperty("status", out var statusElement))
                {
                    return statusElement.GetString() ?? "Hasil berhasil dikirim";
                }
                return "Hasil berhasil dikirim";
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return "Error parsing response";
            }
        }

        // Fungsi untuk membuat HttpClient dengan timeout yang lebih lama
        public static HttpClient CreateHttpClient(AccountInfo accountInfo)
        {
            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy,
                    UseProxy = true
                };
            }

            HttpClient client = new(httpClientHandler);
            // Set timeout menjadi 300 detik (5 menit)
            client.Timeout = TimeSpan.FromSeconds(300);

            return client;
        }

        // Fungsi untuk menghitung hash anti-cheat untuk Spritetype
        static string ComputeAntiCheatHash(string e, int t, int a, int r, int s, int i)
        {
            int l = s + i;
            long n = 0 + 23 * t + 89 * a + 41 * r + 67 * s + 13 * i + 97 * l;
            long o = 0;
            for (int idx = 0; idx < e.Length; idx++)
                o += e[idx] * (idx + 1);

            var c = Math.Floor((double)0x178ba57548d * (n += 31 * o) % 9007199254740991);
            string result = $"{e.ToLower()}_{t}_{a}_{r}_{s}_{i}_{c}";
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(result);
                byte[] hash = sha256.ComputeHash(bytes);
                string hashstring = BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 32);
                return hashstring;
            }
        }

        // Fungsi untuk memuat akun dan proxy
        public static void LoadAccountsAndProxy()
        {
            // Cek dan buat file jika tidak ada
            if (!File.Exists("Address.txt"))
                File.Create("Address.txt").Close();
            if (!File.Exists("PrivateKeys.txt"))
                File.Create("PrivateKeys.txt").Close();
            if (!File.Exists("Proxy.txt"))
                File.Create("Proxy.txt").Close();

            string[] addresses = File.ReadAllLines("Address.txt");
            string[] privateKeys = File.ReadAllLines("PrivateKeys.txt");
            string[] proxy = File.ReadAllLines("Proxy.txt");

            if (addresses.Length == 0 && privateKeys.Length == 0)
            {
                ShowMsg("Tidak ada informasi akun yang tersedia, program akan segera keluar!", 3);
                Thread.Sleep(3000);
                Environment.Exit(0);
            }

            AccountInfoList.Clear();

            // Proses alamat wallet
            for (int i = 0; i < addresses.Length; i++)
            {
                string address = addresses[i].Trim();
                if (string.IsNullOrWhiteSpace(address))
                    continue;

                try
                {
                    address = AddressUtil.Current.ConvertToChecksumAddress(address);

                    // Cek jika sudah ada akun dengan alamat ini
                    var existingAccount = AccountInfoList.FirstOrDefault(a => a.Address == address);
                    if (existingAccount == null)
                    {
                        AccountInfoList.Add(new AccountInfo
                        {
                            Index = AccountInfoList.Count + 1,
                            Address = address
                        });
                    }
                }
                catch (Exception ex)
                {
                    ShowMsg($"Alamat tidak valid: {address} ({ex.Message})", 3);
                }
            }

            // Proses private keys
            for (int i = 0; i < privateKeys.Length; i++)
            {
                string privateKey = privateKeys[i].Trim();
                if (string.IsNullOrWhiteSpace(privateKey))
                    continue;

                try
                {
                    // Dapatkan alamat dari private key
                    var ecKey = new EthECKey(privateKey);
                    string address = ecKey.GetPublicAddress();
                    address = AddressUtil.Current.ConvertToChecksumAddress(address);

                    // Cek jika sudah ada akun dengan alamat ini
                    var existingAccount = AccountInfoList.FirstOrDefault(a => a.Address == address);
                    if (existingAccount != null)
                    {
                        // Update private key untuk akun yang sudah ada
                        existingAccount.PrivateKey = privateKey;
                    }
                    else
                    {
                        // Tambahkan akun baru
                        AccountInfoList.Add(new AccountInfo
                        {
                            Index = AccountInfoList.Count + 1,
                            Address = address,
                            PrivateKey = privateKey
                        });
                    }
                }
                catch (Exception ex)
                {
                    ShowMsg($"Private key tidak valid: {privateKey} ({ex.Message})", 3);
                }
            }

            if (AccountInfoList.Count == 0)
            {
                ShowMsg("Tidak ada akun yang valid, program akan keluar!", 3);
                Thread.Sleep(3000);
                Environment.Exit(0);
            }

            ShowMsg($"Telah memuat {AccountInfoList.Count} akun", 1);

            // Tambahkan proxy ke akun
            int proxyLine = proxy.Length;
            for (int i = 0; i < AccountInfoList.Count && i < proxyLine; i++)
            {
                var line = proxy[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    if (line.StartsWith("socks", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowMsg($"Tidak mendukung proxy SOCKS, silakan gunakan proxy Http atau Https, {line}", 2);
                        continue;
                    }

                    var uri = new Uri(
                        line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? line : $"http://{line}"
                    );
                    var webProxy = new WebProxy(uri);

                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var userInfo = uri.UserInfo.Split(':');
                        if (userInfo.Length == 2)
                        {
                            webProxy.Credentials = new NetworkCredential(userInfo[0], userInfo[1]);
                        }
                    }

                    AccountInfoList[i].Proxy = webProxy;
                }
                catch (Exception ex)
                {
                    ShowMsg($"Format proxy salah: {line} ({ex.Message})", 3);
                }
            }

            int proxyCount = AccountInfoList.Count(x => x.Proxy is not null);
            ShowMsg($"Telah memuat {proxyCount} proxy", proxyCount > 0 ? 1 : 2);
        }

        // Fungsi untuk menandatangani pesan
        public static string SignMessage(string privateKey, string message)
        {
            try
            {
                ShowMsg($"Pesan yang akan ditandatangani: {message}", 1);

                // Gunakan metode penandatanganan yang benar
                var signer = new EthereumMessageSigner();
                string signature = signer.Sign(Encoding.UTF8.GetBytes(message), new EthECKey(privateKey));

                ShowMsg($"Tanda tangan yang dihasilkan: {signature}", 1);

                return signature;
            }
            catch (Exception ex)
            {
                ShowMsg($"Error saat menandatangani pesan: {ex.Message}", 3);
                throw;
            }
        }

        // Fungsi untuk generate string acak
        public static string GenerateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Fungsi untuk generate user agent acak
        public static string GetRandomUserAgent()
        {
            Random random = new();
            int revisionVersion = random.Next(1, 8000);
            int tailVersion = random.Next(1, 150);
            return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.{revisionVersion}.{tailVersion} Safari/537.36";
        }

        // Fungsi untuk menampilkan pesan
        public static void ShowMsg(string msg, int logLevel)
        {
            string logFile = $"Log.txt";
            string logText = $"{DateTime.Now} - {msg}\n";
            ConsoleColor color = ConsoleColor.White;

            switch (logLevel)
            {
                case 1:
                    color = ConsoleColor.Green;
                    msg = " ✔   " + msg;
                    break;
                case 2:
                    color = ConsoleColor.DarkYellow;
                    msg = " ⚠   " + msg;
                    break;
                case 3:
                    color = ConsoleColor.Red;
                    msg = " ❌   " + msg;
                    break;
            }

            lock (_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(msg);
                Console.ResetColor();
                File.AppendAllText(logFile, logText);
            }
        }

        // Kelas untuk informasi akun
        public class AccountInfo
        {
            public int Index { get; set; }
            public string Address { get; set; } = string.Empty;
            public string PrivateKey { get; set; } = string.Empty;
            public WebProxy? Proxy { get; set; }
            public string UserAgent { get; set; } = GetRandomUserAgent();
            public int FailTime { get; set; }
            public DateTime NextExecutionTime { get; set; } = DateTime.MinValue;
        }

        // Kelas untuk hasil start game
        public class GameStartResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string SessionId { get; set; } = string.Empty;
            public string SessionRecordUrl { get; set; } = string.Empty;
            public string TransactionHash { get; set; } = string.Empty;
            public decimal GameCostDeducted { get; set; }
            public int BlockNumber { get; set; }
        }
    }
}