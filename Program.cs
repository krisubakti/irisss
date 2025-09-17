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
        public static decimal GameCost = 0.001m; // Biaya permainan Snake dalam IRYS
        public static int GameType = 0; // 0: Spritetype, 1: Snake, 2: Keduanya

        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            LoadAccountsAndProxy();

            Console.Write("Silakan pilih game yang ingin dijalankan (0: Spritetype, 1: Snake, 2: Keduanya): ");
            var inputGameType = Console.ReadLine();
            if (int.TryParse(inputGameType, out int gameType) && (gameType == 0 || gameType == 1 || gameType == 2))
                GameType = gameType;

            Console.Write("Silakan masukkan berapa kali setiap akun akan mengeksekusi skrip: ");
            var inputCount = Console.ReadLine();
            if (int.TryParse(inputCount, out int count) && count > 0)
                ScriptRunCount = count;

            Console.Write("Silakan pilih mode operasi (1. Mode Aman 2. Mode Cepat): ");
            var inputMode = Console.ReadLine();
            if (int.TryParse(inputMode, out int mode) && (mode == 1 || mode == 2))
                RunMode = mode;

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

        // Game Snake (memerlukan private key)
        public static async Task RunSnakeGame(AccountInfo accountInfo)
        {
            ShowMsg($"Menjalankan game Snake untuk akun {accountInfo.Index} - {accountInfo.Address}", 0);
            
            for (int i = 0; i < ScriptRunCount; i++)
            {
                try
                {
                    // Mulai game
                    string startResult = await StartSnakeGame(accountInfo);
                    ShowMsg($"Memulai game Snake ke-{i + 1}: " + startResult, 1);
                    
                    // Jika berhasil memulai game, tunggu beberapa detik lalu kirim hasil
                    if (startResult.Contains("successful") || startResult.Contains("berhasil"))
                    {
                        // Generate session ID
                        string sessionId = $"game_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{GenerateRandomString(8)}";
                        
                        // Tunggu beberapa detik untuk mensimulasikan permainan
                        int gameDuration = new Random().Next(10, 60);
                        ShowMsg($"Menunggu {gameDuration} detik untuk mensimulasikan permainan Snake...", 1);
                        Thread.Sleep(gameDuration * 1000);
                        
                        // Generate score yang realistis
                        int score = new Random().Next(10, 500);
                        
                        // Kirim hasil game
                        string submitResult = await SubmitSnakeGameResult(accountInfo, sessionId, score);
                        ShowMsg($"Mengirim hasil game Snake ke-{i + 1} (score: {score}): " + submitResult, 1);
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

        // Fungsi untuk game Spritetype (dari script asli)
        public static async Task<string> Spritetype(AccountInfo accountInfo)
        {
            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy
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

        // Fungsi untuk memulai game Snake
        public static async Task<string> StartSnakeGame(AccountInfo accountInfo)
        {
            string sessionId = $"game_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{GenerateRandomString(8)}";
            string message = $"I authorize payment of {GameCost} IRYS to play a game on Irys Arcade.\n    \nPlayer: {accountInfo.Address}\nAmount: {GameCost} IRYS\nTimestamp: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}\n\nThis signature confirms I own this wallet and authorize the payment.";
            string signature = SignMessage(accountInfo.PrivateKey, message);
            
            var payload = new
            {
                playerAddress = accountInfo.Address,
                gameCost = GameCost,
                signature,
                message,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sessionId,
                gameType = "snake"
            };
            
            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy
                };
            }
            
            using HttpClient client = new(httpClientHandler);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/start");
            
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.6");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("referer", "https://play.irys.xyz/snake");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", accountInfo.UserAgent);
            
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
                return "Pengiriman gagal";
            }
            
            string responseBody = await response.Content.ReadAsStringAsync();
            
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
            
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? "Game berhasil dimulai";
                }
                return "Game berhasil dimulai";
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return "Error parsing response";
            }
        }

        // Fungsi untuk mengirim hasil game Snake
        public static async Task<string> SubmitSnakeGameResult(AccountInfo accountInfo, string sessionId, int score)
        {
            var rand = new Random();
            int gameDuration = rand.Next(60, 300);
            int foodEaten = rand.Next(5, 50);
            int maxSnakeLength = rand.Next(10, 100);
            
            var payload = new
            {
                playerAddress = accountInfo.Address,
                sessionId,
                gameStats = new
                {
                    score,
                    gameDuration,
                    foodEaten,
                    maxSnakeLength
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            HttpClientHandler httpClientHandler = new();
            if (accountInfo.Proxy is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = accountInfo.Proxy
                };
            }
            
            using HttpClient client = new(httpClientHandler);
            HttpRequestMessage request = new(HttpMethod.Post, "https://play.irys.xyz/api/game/submit");
            
            request.Headers.Add("accept", "*/*");
            request.Headers.Add("accept-language", "id-ID,id;q=0.6");
            request.Headers.Add("cache-control", "no-cache");
            request.Headers.Add("origin", "https://play.irys.xyz");
            request.Headers.Add("pragma", "no-cache");
            request.Headers.Add("referer", "https://play.irys.xyz/snake");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Brave\";v=\"140\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-gpc", "1");
            request.Headers.Add("user-agent", accountInfo.UserAgent);
            
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
                ShowMsg($"Pengiriman hasil gagal: " + ex.Message, 3);
                return "Pengiriman hasil gagal";
            }
            
            string responseBody = await response.Content.ReadAsStringAsync();
            
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
                return "Hasil berhasil dikirim";
            }
            catch (Exception ex)
            {
                ShowMsg($"Error parsing response: {ex.Message}", 3);
                return "Error parsing response";
            }
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

        // Fungsi untuk menandatangani pesan (DIPERBAIKI)
        public static string SignMessage(string privateKey, string message)
        {
            try
            {
                var ecKey = new EthECKey(privateKey);
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var hash = new Sha3Keccack().CalculateHash(messageBytes);
                var signature = ecKey.Sign(hash);
                return signature.ToHex();
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
    }
}