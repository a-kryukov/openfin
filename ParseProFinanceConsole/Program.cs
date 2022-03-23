// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Text;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();

async static Task MainAsync(string[] args)
{
    IConfiguration Config = new ConfigurationBuilder()
        .AddJsonFile("settings.json")
        .Build();
				
    var path = Config.GetSection("FilesPath").Value;
    var tickers = Config.GetSection("Tickers").Value.Split(",");

    var dict = new Dictionary<string, string>();
    
    var session = new CookieContainer();
    HttpClientHandler clienthandler = new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = session };

    using (var client = new HttpClient(clienthandler))
    {
        client.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
        var response = await client.GetAsync("https://jq.profinance.ru/html/htmlquotes/site.jsp");
        var pageContent = await response.Content.ReadAsStringAsync();
        
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(pageContent);
        var frame = doc.DocumentNode.SelectSingleNode("//iframe");
        if (frame != null)
        {
            var url = frame.Attributes["src"].Value;
            if (url != null)
            {
                response = await client.GetAsync("https://jq.profinance.ru/html/htmlquotes/" + url);
                pageContent = await response.Content.ReadAsStringAsync();
                var sid = url.Split("SID=")[1].Split("&")[0];
                var time = GetTime();
                var streamUrl = $"https://jq.profinance.ru/html/htmlquotes/qsse?msg=1;SID={sid};T={time}";
                Console.WriteLine(streamUrl);
                pageContent = pageContent.Replace("\n", " ").Replace("\t","").Replace(" ","");
                var start = pageContent.IndexOf("newArray") + 9;
                var end = pageContent.IndexOf(");");
                var res = pageContent.Substring(start,
                    end-start);
                var ar = res.Split(",");
                var sb = new StringBuilder();
                foreach (var a in ar)
                {
                    sb.AppendLine($"1;SID={sid};{a}");
                }
                
                var data = sb.ToString().Replace("\"","");
                StringContent queryString = new StringContent(data);
                response = await client.PostAsync("https://jq.profinance.ru/html/htmlquotes/q", queryString);
                pageContent = await response.Content.ReadAsStringAsync();
                
                
                Stream stream = await client.GetStreamAsync(streamUrl);

                string alum = string.Empty;
                string copper = string.Empty;
                string nickel = string.Empty;
                
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var r = reader.ReadLine();
                        //Console.WriteLine(r);
                        
                        var dA = r.Split(";");
                        if (dA.Length > 7)
                        {
                            var s = dA[2].Split("=")[1];

                            foreach (var ticker in tickers)
                            {
                                if (s.ToLower() == ticker.ToLower())
                                {
                                    data = getFormattedData(dA);
                                    if (dict.ContainsKey(ticker))
                                    {
                                        dict[ticker] = data;
                                    }
                                    else
                                        dict.Add(ticker, data);
                                    
                                    await File.WriteAllTextAsync($"{path}\\{ticker}.csv", data);
                                }
                            }
                        }
                        
                        Console.Clear();
                        foreach (var ticker in tickers)
                        {
                            if (dict.ContainsKey(ticker))
                                Console.WriteLine($"{ticker}: {dict[ticker]}");    
                        }
                        
                    }
                }
            }
        }
    }
    
    
    Console.ReadLine();
}

static string getFormattedData(string[] dA)
{
    var b = dA[4].Split("=")[1];
    var a = dA[7].Split("=")[1];
    var t = dA[8].Split("=")[1];
    //var nch = dA[5].Split("=")[1];
    //var nchp = dA[6].Split("=")[1];

    return $"{b.Trim('+').Trim('-')};{a.Trim('+').Trim('-')};{t}";
}

static long GetTime()
{
    return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
}