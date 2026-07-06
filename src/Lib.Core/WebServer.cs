// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2026 AirVPN (support@airvpn.org) / https://airvpn.org
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Eddie.Core
{
	public class Webserver
	{
		public string ListenUrl;

		private HttpListener m_listener = new HttpListener();

		//private List<Json> m_pullItems = new List<Json>();

		private WebserverClient m_client = new WebserverClient();

		private const string SessionCookieName = "eddie_session";
		private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);
		private readonly Dictionary<string, DateTime> m_sessions = new Dictionary<string, DateTime>();

		public static string GetPath()
		{
			string pathRoot = Platform.Instance.NormalizePath(Engine.Instance.LocateResource("webui"));
			if (pathRoot != "")
				return pathRoot;
			else
				return "";
		}

		public void Init(string prefix)
		{
			Engine.Instance.UiManager.Add(m_client);

			if (GetPath() == "")
				return;

			if (!HttpListener.IsSupported)
				throw new NotSupportedException("Needs Windows XP SP2, Server 2003 or later.");

			m_listener.Prefixes.Add(prefix);

			m_listener.Start();
		}

		public void Run()
		{
			ThreadPool.QueueUserWorkItem((o) =>
			{
				try
				{
					while (m_listener.IsListening)
					{
						ThreadPool.QueueUserWorkItem((c) =>
						{
							HttpListenerContext context = c as HttpListenerContext;
							try
							{
								SendResponse(context);
							}
							catch (Exception ex)
							{
								Engine.Instance.Logs.Log(ex);
								context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
							}
							finally
							{
								context.Response.OutputStream.Close();
							}
						}, m_listener.GetContext());
					}
				}
				catch (Exception)
				{
				}
			});
		}

		void WriteFile(HttpListenerContext ctx, string path, bool asDownload)
		{
			HttpListenerResponse response = ctx.Response;
			using (FileStream fs = File.OpenRead(path))
			{
				string filename = Path.GetFileName(path);
				//response is HttpListenerContext.Response...
				response.ContentLength64 = fs.Length;
				response.SendChunked = false;
				response.AddHeader("Last-Modified", File.GetLastWriteTime(path).ToString("r"));
				if (asDownload)
				{
					response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
					response.AddHeader("Content-disposition", "attachment; filename=" + filename);
				}
				else
				{
					string mime = "";
					int posLastDot = path.LastIndexOf('.');
					if (posLastDot != -1)
					{
						string ext = path.Substring(posLastDot + 1);
						if (Engine.Instance.Manifest["mime_types"]["extension_to_type"].Json.HasKey(ext))
							mime = Engine.Instance.Manifest["mime_types"]["extension_to_type"][ext].Value as string;
						else
							mime = Engine.Instance.Manifest["mime_types"]["extension_to_type"]["*"].Value as string;
					}

					if (mime != "")
						response.ContentType = mime;

					if ((mime.StartsWithInv("text/")) || (mime == "application/javascript"))
						response.ContentEncoding = Encoding.UTF8;
				}

				response.StatusCode = (int)HttpStatusCode.OK;
				response.StatusDescription = "OK";

				byte[] buffer = new byte[64 * 1024];
				int read;
				using (BinaryWriter bw = new BinaryWriter(response.OutputStream))
				{
					while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
					{
						bw.Write(buffer, 0, read);
						bw.Flush(); //seems to have no effect
					}

					bw.Close();
				}


				response.OutputStream.Close();
			}
		}

		public void Stop()
		{
			m_listener.Stop();
			m_listener.Close();
		}

		public void Start()
		{
			int port = Engine.Instance.ProfileOptions.GetInt("webui.port");
			ListenUrl = "http://localhost:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture); // Note: bind to localhost, not 127.0.0.1, otherwise HttpListener throws "Access is denied".
			Init(ListenUrl + "/");
			Run();
		}

		public void SendResponse(HttpListenerContext context)
		{
			// string physicalPath = GetPath() + request.RawUrl;
			string bodyResponse = ""; // If valorized, always a dynamic response
			Dictionary<string, string> requestHeaders = new Dictionary<string, string>();
			foreach (string key in context.Request.Headers.AllKeys)
				requestHeaders[key.ToLowerInvariant()] = context.Request.Headers[key];
			string requestHttpMethod = context.Request.HttpMethod.ToLowerInvariant().Trim();

			context.Response.Headers["Server"] = Constants.Name + " " + Engine.Instance.GetVersionShow();
			context.Response.Headers["Access-Control-Allow-Origin"] = ListenUrl;
			context.Response.Headers["Vary"] = "Origin";

			foreach (KeyValuePair<string, object> jsonHeader in Engine.Instance.Manifest["webserver"]["headers"]["common"].Json.GetDictionary())
			{
				string k = jsonHeader.Key;
				string v = (jsonHeader.Value as string);
				context.Response.Headers[k] = v;
			}

			string origin = context.Request.Headers["Origin"];
			if ((requestHeaders.ContainsKey("origin")) && (requestHeaders["origin"].StartsWithInv(ListenUrl) == false))
			{
				List<string> hostsAllowed = new List<string>(); // Option?
				hostsAllowed.Add("127.0.0.1");
				hostsAllowed.Add("localhost");
				Uri uriOrigin = new Uri(requestHeaders["origin"]);
				if (hostsAllowed.Contains(uriOrigin.Host) == false)
				{
					context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
					return;
				}
			}

			// Anti DNS-rebinding: only accept requests addressed to a loopback Host.
			if (CheckHost(requestHeaders) == false)
			{
				context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
				return;
			}

			if (requestHttpMethod == "options")
			{
				Engine.Instance.Logs.LogVerbose(origin);
				context.Response.StatusCode = (int)HttpStatusCode.NoContent;
				return;
			}

			string absolutePath = context.Request.Url.AbsolutePath;

			if (absolutePath == "/api/login")
			{
				bodyResponse = HandleLogin(context);
			}
			else if ((absolutePath == "/api/command/") || (absolutePath == "/api/pull/"))
			{
				if (IsAuthenticated(context) == false)
				{
					context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
					return;
				}

				if (absolutePath == "/api/command/")
				{
					if (requestHttpMethod == "post")
					{
						string data = new StreamReader(context.Request.InputStream).ReadToEnd();
						Json ret = Receive(data);
						if (ret != null)
							bodyResponse = ret.ToJson();
						else
							bodyResponse = "null";
					}
					else
					{
						context.Response.StatusCode = (int)HttpStatusCode.NoContent;
					}
				}
				else // /api/pull/
				{
					if (requestHttpMethod == "post")
					{
						lock (m_client.Pendings)
						{
							if (m_client.Pendings.Count == 0)
							{
								bodyResponse = "null";
							}
							else
							{
								Json data = m_client.Pendings[0];
								m_client.Pendings.RemoveAt(0);
								bodyResponse = data.ToJson();
							}
						}
					}
					else
					{
						context.Response.StatusCode = (int)HttpStatusCode.NoContent;
					}
				}
			}
			else
			{
				string urlPath = context.Request.Url.LocalPath;
				if (urlPath == "/")
					urlPath = IsAuthenticated(context) ? "/index.html" : "/login.html";
				string localPath = GetPath() + urlPath;
				if (Platform.Instance.FileExists(localPath))
				{
					if (context.Request.HttpMethod == "GET")
						WriteFile(context, localPath, false);
					else
						throw new Exception("Unexpected");
				}
				else
				{
					context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				}
			}

			if (bodyResponse != "") // Always dynamic
			{
				context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
				context.Response.Headers["Pragma"] = "no-cache";

				byte[] buf = Encoding.UTF8.GetBytes(bodyResponse);
				context.Response.ContentLength64 = buf.Length;
				context.Response.OutputStream.Write(buf, 0, buf.Length);
			}
		}

		public Json Receive(string data)
		{
			Json jData = Json.Parse(data);
			Json command = jData["data"].Value as Json;

			// Defense in depth: WebServer clients cannot reconfigure the WebServer itself
			// (the authoritative check is in UiManager, keyed on the sender).
			if (command != null)
			{
				string cmd = command["command"].Value as string;
				if ((cmd == "options.set") && (command["name"].ValueString.StartsWithInv("webui.")))
					return null;
			}

			return m_client.Command(command);
		}

		private string HandleLogin(HttpListenerContext context)
		{
			if (context.Request.HttpMethod.ToUpperInvariant() != "POST")
			{
				context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
				return "";
			}

			string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
			string submittedKey = ParseKeyFromBody(body, context.Request.ContentType);

			string accessKey = Engine.Instance.ProfileOptions.Get("webui.access_key");
			if ((accessKey != "") && Crypto.Manager.FixedTimeEquals(submittedKey, accessKey))
			{
				string sessionId = CreateSession();
				context.Response.Headers.Add("Set-Cookie", SessionCookieName + "=" + sessionId + "; HttpOnly; SameSite=Strict; Path=/");
				context.Response.Redirect("/");
				return "";
			}

			context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
			context.Response.ContentType = "text/html; charset=utf-8";
			return "<!DOCTYPE html><meta charset=\"utf-8\"><body style=\"font-family:sans-serif\"><p>Invalid access key. <a href=\"/\">Try again</a></p></body>";
		}

		private static string ParseKeyFromBody(string body, string contentType)
		{
			if (body == null)
				return "";
			body = body.Trim();

			if ((contentType != null) && (contentType.ToLowerInvariant().Contains("application/json")))
			{
				try
				{
					Json j = Json.Parse(body);
					return j["key"].ValueString;
				}
				catch (Exception)
				{
					return "";
				}
			}

			// application/x-www-form-urlencoded
			foreach (string pair in body.Split('&'))
			{
				string[] kv = pair.Split(new char[] { '=' }, 2);
				if (kv.Length != 2)
					continue;
				if (Uri.UnescapeDataString(kv[0]) == "key")
					return Uri.UnescapeDataString(kv[1].Replace('+', ' '));
			}

			return "";
		}

		private bool IsAuthenticated(HttpListenerContext context)
		{
			string accessKey = Engine.Instance.ProfileOptions.Get("webui.access_key");
			if (accessKey == "")
				return false; // Fail closed if no key is configured.

			// Bearer token (scripts / API clients)
			string auth = context.Request.Headers["Authorization"];
			if ((auth != null) && (auth.StartsWithInv("Bearer ")))
			{
				string token = auth.Substring("Bearer ".Length).Trim();
				if (Crypto.Manager.FixedTimeEquals(token, accessKey))
					return true;
			}

			// Session cookie (browser)
			Cookie cookie = context.Request.Cookies[SessionCookieName];
			if ((cookie != null) && (cookie.Value != ""))
			{
				lock (m_sessions)
				{
					DateTime expiry;
					if (m_sessions.TryGetValue(cookie.Value, out expiry))
					{
						if (expiry > DateTime.UtcNow)
						{
							m_sessions[cookie.Value] = DateTime.UtcNow.Add(SessionLifetime); // sliding renewal
							return true;
						}

						m_sessions.Remove(cookie.Value);
					}
				}
			}

			return false;
		}

		private string CreateSession()
		{
			string id = RandomGenerator.GetHash();
			lock (m_sessions)
				m_sessions[id] = DateTime.UtcNow.Add(SessionLifetime);
			return id;
		}

		public void ClearSessions()
		{
			lock (m_sessions)
				m_sessions.Clear();
		}

		private static bool CheckHost(Dictionary<string, string> requestHeaders)
		{
			// Intentional: accept missing Host header.
			if (requestHeaders.ContainsKey("host") == false)
				return true;

			string host = requestHeaders["host"];
			int posColon = host.LastIndexOf(':');
			if (posColon >= 0)
				host = host.Substring(0, posColon);
			host = host.Trim().Trim('[', ']'); // strip IPv6 brackets

			return (host == "localhost") || (host == "127.0.0.1") || (host == "::1");
		}
	}
}