﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DnsTools.Worker.Utils;

namespace DnsTools.Worker.Tools
{
	/// <summary>
	/// Wraps ping CLI
	/// </summary>
	public class Ping : BaseCliTool<PingRequest, PingResponse>
	{
		private static readonly Regex _timeoutRegex = new Regex(@"^no answer yet for", RegexOptions.Compiled);

		private static readonly Regex _bytesRegex = new Regex(@"(?<bytes>[0-9]+) bytes from", RegexOptions.Compiled);
		private static readonly Regex _seqRegex = new Regex(@"seq=(?<seq>[0-9]+)\b", RegexOptions.Compiled);
		private static readonly Regex _ttlRegex = new Regex(@"ttl=(?<ttl>[0-9]+)\b", RegexOptions.Compiled);
		private static readonly Regex _rttRegex = new Regex(@"time=(?<rtt>[0-9\.]+) ms\b", RegexOptions.Compiled);

		private static readonly Regex _sentRegex = new Regex(@"(?<sent>[0-9]+) packets transmitted", RegexOptions.Compiled);
		private static readonly Regex _receivedRegex = new Regex(@"(?<received>[0-9]+) received", RegexOptions.Compiled);

		protected override string GetCommand(PingRequest request) => "ping";

		protected override IReadOnlyList<string> GetArguments(PingRequest request)
		{
			Hostname.AssertValid(request.Host);
			var args = new List<string> { "-i 0.5", "-c 5", "-O", request.Host };
			switch (request.Protocol)
			{
				case Protocol.Any:
					break;

				case Protocol.Ipv4:
					args.Add("-4");
					break;

				case Protocol.Ipv6:
					args.Add("-6");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			return args;
		}

		protected override PingResponse ParseResponse(string reply)
		{
			var seqMatch = _seqRegex.Match(reply);
			
			if (_timeoutRegex.IsMatch(reply))
			{
				return new PingResponse
				{
					Timeout = new PingTimeout
					{
						Seq = int.Parse(seqMatch.Groups["seq"].Value),
					},
				};
			}

			var bytesMatch = _bytesRegex.Match(reply);
			var rttMatch = _rttRegex.Match(reply);
			var ttlMatch = _ttlRegex.Match(reply);
			if (bytesMatch.Success && seqMatch.Success && rttMatch.Success && ttlMatch.Success)
			{
				return new PingResponse
				{
					Reply = new PingReply
					{
						Bytes = int.Parse(_bytesRegex.Match(reply).Groups["bytes"].Value),
						RawReply = reply,
						Rtt = float.Parse(_rttRegex.Match(reply).Groups["rtt"].Value),
						Seq = int.Parse(_seqRegex.Match(reply).Groups["seq"].Value),
						Ttl = int.Parse(_ttlRegex.Match(reply).Groups["ttl"].Value),
					}
				};
			}

			var sentMatch = _sentRegex.Match(reply);
			var receivedMatch = _receivedRegex.Match(reply);
			if (sentMatch.Success && receivedMatch.Success)
			{
				return new PingResponse
				{
					Summary = new PingSummary
					{
						RawReply = reply,
						Received = int.Parse(receivedMatch.Groups["received"].Value),
						Sent = int.Parse(sentMatch.Groups["sent"].Value),
					}
				};
			}

			return null;
		}
	}
}
