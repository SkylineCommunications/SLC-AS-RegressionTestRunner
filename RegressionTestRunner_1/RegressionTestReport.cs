namespace RegressionTestRunner
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Trending;
	using SLNetMessages = Skyline.DataMiner.Net.Messages;

	public class RegressionTestReport
	{
		private readonly string clusterName;
		private readonly IEnumerable<RegressionTestResult> results;
		private readonly DateTime creationTime = DateTime.Now;
		private readonly int dmaId;
		private readonly int elementId;
		private readonly GetTrendDataResponseMessage response;
		private readonly GetTrendDataResponseMessage averageRate;

		public RegressionTestReport(IEngine engine, IEnumerable<RegressionTestResult> results)
		{
			if (engine == null) throw new ArgumentNullException(nameof(engine));
			this.results = results ?? throw new ArgumentNullException(nameof(results));

			var element = engine.FindElementsByProtocol("Skyline Regression Test Result Collector").FirstOrDefault();
			elementId = element.ElementId;
			dmaId = element.DmaId;

			var dataminerInfoMessage = new GetInfoMessage(SLNetMessages.InfoType.DataMinerInfo);
			var infoMessageResponse = engine.SendSLNetMessage(dataminerInfoMessage)[0] as GetDataMinerInfoResponseMessage;
			clusterName = infoMessageResponse.Cluster;

			string[] retTablePk = element.GetTablePrimaryKeys((int)Parameters.RegressionTestState);
			ParameterIndexPair[] parametersToRequest = retTablePk.Select((pk, index) => new ParameterIndexPair
			{
				ID = 102,
				Index = pk,
			}).ToArray();

			ParameterIndexPair[] parametersToRequestAverage = new[]
			{
				new ParameterIndexPair { ID = 200 },
			};

			var trendDataMessage = new GetTrendDataMessage
			{
				AverageTrendIntervalType = AverageTrendIntervalType.Auto,
				DataMinerID = dmaId,
				ElementID = elementId,
				EndTime = creationTime,
				Parameters = parametersToRequest,
				RetrievalWithPrimaryKey = true,
				ReturnAsObjects = true,
				SkipCache = false,
				StartTime = creationTime.AddDays(-6),
				TrendingType = TrendingType.Average,
			};

			var trendOverallMessage = new GetTrendDataMessage
			{
				AverageTrendIntervalType = AverageTrendIntervalType.Auto,
				DataMinerID = dmaId,
				ElementID = elementId,
				EndTime = creationTime,
				Parameters = parametersToRequestAverage,
				RetrievalWithPrimaryKey = false,
				ReturnAsObjects = true,
				SkipCache = false,
				StartTime = creationTime.AddDays(-6),
				TrendingType = TrendingType.Average,
			};

			averageRate = engine.SendSLNetMessage(trendOverallMessage)[0] as GetTrendDataResponseMessage;
			response = engine.SendSLNetMessage(trendDataMessage)[0] as GetTrendDataResponseMessage;
		}

		public enum Status
		{
			Fail = 0,
			Ok = 1,
			Unknown = 2,
		}

		public enum Parameters
		{
			RegressionTestState = 102,
		}

		public string Title
		{
			get
			{
				return $"Regression Test Results - {clusterName} [{creationTime}]";
			}
		}

		public double SuccessRate
		{
			get
			{
				return Math.Round((double)results.Count(x => !x.Success) / (double)results.Count() * 100d);
			}
		}

		public string Body
		{
			get
			{
				var sb = new StringBuilder();
				var testsSucceed = results.Where(x => x.Success).ToList();
				var testsFailed = results.Where(x => !x.Success).ToList();
				List<double> failureRateList = new List<double>();
				sb.AppendLine(GenerateParagraph($"Overall Failure Rate Last Run: {SuccessRate} %"));
				sb.AppendLine("ReplaceAfter");
				AddRegressionTestTable(sb, testsFailed, "Failed Regression Tests", failureRateList);
				AddFailedReasonTestTable(sb);
				AddRegressionTestTable(sb, testsSucceed, "Passed Regression Tests", failureRateList);
				sb.AppendLine(GenerateLogLink("SLC-H62-G05"));

				return sb.ToString().Replace("ReplaceAfter", GenerateParagraph($"Overall Failure Rate Longer Duration: {Math.Round(failureRateList.Average(), 2)} %"));
			}
		}

		public static string JoinStrings(IEnumerable<string> parts, string delimiter)
		{
			return string.Join(delimiter, parts);
		}

		private static string GenerateParagraph(string content)
		{
			return $"<p style=\"margin: 10px;font-size:18px\">{content}</p>";
		}

		private string GenerateLogLink(string serverName)
		{
			string monthName = DateTime.Now.ToString("MMMM", CultureInfo.InvariantCulture);
			string folderPath = $@"\\{serverName}\RegressionTestLogResults\{monthName}";
			string fileName = $"{monthName}_{DateTime.Now:dd}_LogFile.txt";
			string link = Path.Combine(folderPath, fileName);

			return $"<p>Test log result can be found on server ({serverName}) here: <a href=\"{link}\">{link}</a></p>";
		}

		private void AddFailedTestCase(StringBuilder sb, string[] reasonParts, RegressionTestResult result)
		{
			if (!result.Success)
			{
				sb.AppendLine("<tr>");
				sb.AppendLine($"<td style=\"color: white; background-color: red; white-space: nowrap;\">{result.Script}</td>");
				sb.AppendLine($"<td>{JoinStrings(reasonParts, "<br>")}</td>");
				sb.AppendLine("</tr>");
			}
		}

		private void AddFailedReasonTestTable(StringBuilder sb)
		{
			sb.AppendLine("<table style=\"width:100%\">");
			sb.AppendLine("<tr><th>Failed Test Case Name</th><th>Reason of Failure</th></tr>");

			foreach (var result in results.GroupBy(x => x.Script))
			{
				var currentEntry = result.OrderByDescending(x => x.CreationTime).FirstOrDefault();

				if (currentEntry != null)
				{
					var reasonLines = currentEntry.Reason.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
					var filteredReasons = reasonLines.Where(x => !x.Contains("Success")).ToArray();
					AddFailedTestCase(sb, filteredReasons, currentEntry);
				}
			}

			sb.AppendLine("</table>");
		}

		private void AddRegressionTestTable(StringBuilder sb, List<RegressionTestResult> testResults, string header, List<double> failureRateList)
		{
			sb.AppendLine($"<h2 style=\"font-size:1.5em\">{header}</h2>");
			sb.AppendLine("<table style=\"width:100%\">");
			sb.AppendLine("<tr><th>Test</th><th>State</th><th>Failure Rate (Longer Duration - 7 days)</th><th>Time</th></tr>");

			var trendResultDict = response.Records;

			foreach (var result in testResults.OrderBy(x => x.Script))
			{
				var failureRate = CalculateFailureRate(trendResultDict, result);
				failureRateList.Add(failureRate);
				sb.AppendLine("<tr>");
				sb.AppendLine($"<td>{result.Script}</td>");
				sb.AppendLine($"<td style=\"color:white;{(result.Success ? "background:green" : "background:red")}\">{(result.Success ? "OK" : "Fail")}</td>");
				sb.AppendLine($"<td>{failureRate} %</td>");
				sb.AppendLine($"<td>{result.CreationTime}</td>");
				sb.AppendLine("</tr>");
			}

			sb.AppendLine("</table>");
		}

		private double CalculateFailureRate(Dictionary<string, List<TrendRecord>> trendResultDict, RegressionTestResult result)
		{
			var today = DateTime.Now.Date;
			var sevenDaysAgo = today.AddDays(-6);
			var storedValues = new Dictionary<DateTime, int>();

			var processed = 0;

			if (trendResultDict.TryGetValue("102/" + result.Script.ToUpperInvariant(), out var trendResult))
			{
				var firstAvailableDateRange = trendResult.Where(x => x.Status == 5 && x.Time.Date>=sevenDaysAgo && x.Time.Date <= today).OrderBy(x=>x.Time.Date).FirstOrDefault();

				if (firstAvailableDateRange.Time.Date > sevenDaysAgo)
				{
					var firstAvailableDate = Convert.ToInt32(trendResult.Where(x => x.Status == 5).FirstOrDefault().GetStringValue());
					var missingDate = sevenDaysAgo;
					while (missingDate < firstAvailableDateRange.Time.Date)
					{
						storedValues[missingDate] = firstAvailableDate;
						missingDate = missingDate.AddDays(1);
					}
				}

				DateTime? previousDate = null;
				int? previousValue = null;

				foreach (var item in trendResult)
				{
					if (item.Status == 5 && item.Time.Date >= sevenDaysAgo && item.Time.Date <= today)
					{
						var date = item.Time.Date;

						if (previousDate.HasValue)
						{
							var missingDate = previousDate.Value.AddDays(1);
							while (missingDate < date)
							{
								storedValues[missingDate] = previousValue.Value;
								missingDate = missingDate.AddDays(1);
							}
						}

						if (!storedValues.ContainsKey(date))
						{
							processed++;
							int value = Convert.ToInt32(item.GetStringValue());
							storedValues[date] = value;
							previousDate = date;
							previousValue = value;
						}
					}
				}
			}

			var totalFailures = storedValues.Count(x => x.Value == (int)Status.Fail);
			return processed > 0 ? Math.Round((double)totalFailures / 7 * 100d, 2) : 0;
		}
	}
}
