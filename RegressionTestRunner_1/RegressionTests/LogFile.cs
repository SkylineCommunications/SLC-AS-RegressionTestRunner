namespace RegressionTestRunner.RegressionTests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using Newtonsoft.Json;
	using RegressionTestRunner.RegressionTests;

	public class LogFile
	{
		public DateTime CreationTime { get; set; }

		public string Path { get; set; }

		public string Content { get; set; } = String.Empty;

		public string ErrorReason { get; set; }

		public string Reason => GetReason();

		public RegressionTestStates State
		{
			get
			{
				if (String.IsNullOrWhiteSpace(Content)) return RegressionTestStates.Unknown;
				if (Content.Contains("RT_PORTAL_FAIL") || Content.Contains("RT_FAIL")) return RegressionTestStates.Fail;
				return RegressionTestStates.OK;
			}
		}

		private bool PushesToPortal => Content.Contains("RT_PORTAL");

		private string GetReason()
		{
			var reasons = new List<string>();

			string[] logLines = Content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

			string testResultString = $"RT_{(PushesToPortal ? "PORTAL_" : string.Empty)}{(State == RegressionTestStates.Fail ? "FAIL" : "SUCCESS")}";

			var lineIndexesOfTestResults = logLines.Where(line => line.Contains(testResultString)).Select(line => Array.IndexOf(logLines, line));

			foreach (int index in lineIndexesOfTestResults)
			{
				try
				{
					string lineWithPostBody = logLines[index - 1];
					
					int startIndex = lineWithPostBody.IndexOf('{');
					int length = lineWithPostBody.LastIndexOf('}') - startIndex + 1;

					string serializedPost = lineWithPostBody.Substring(startIndex, length);
					var postBody = JsonConvert.DeserializeObject<PostBody>(serializedPost);
					reasons.Add(postBody.Value);
				}
				catch (Exception e)
				{
					reasons.Add($"Unable to deserialize the post message on line: {index + 1} due to {e}");
				}
			}

			return String.Join("\n", reasons);	
		}
	}
}
