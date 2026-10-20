using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BlackHole.Net.Unity;
using NUnit.Framework;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace BlackHole.Net.Tests
{
    // 전송이 결과를 셋(서버가 답함 / 네트워크 오류 / 타임아웃)으로 나누고, 서버 오류 본문을 읽는지(계약 2.4).
    // 분류는 ToResult로 네트워크 없이 보고, 헤더·타임아웃·연결 실패는 로컬 소켓으로 실제 요청을 보내 본다.
    public sealed class UnityHttpTransportTests
    {
        private const string TokenBody = "{\"accessToken\":\"a\",\"expiresIn\":900,\"refreshToken\":\"r\"}";

        [Test]
        public void SuccessHasBodyAndNoError()
        {
            HttpResult result = UnityHttpTransport.ToResult(UnityWebRequest.Result.Success, 200, null, TokenBody);

            Assert.AreEqual(HttpOutcome.Response, result.Outcome);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(TokenBody, result.Body);
            Assert.IsNull(result.Error);
        }

        [Test]
        public void ServerErrorBodyIsParsed()
        {
            string body = FakeHttpTransport.ErrorBody(ApiErrorCodes.InvalidCredentials, "틀림");

            HttpResult result = UnityHttpTransport.ToResult(UnityWebRequest.Result.ProtocolError, 401, "HTTP/1.1 401 Unauthorized", body);

            Assert.IsTrue(result.Reached);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(401, result.StatusCode);
            Assert.AreEqual(ApiErrorCodes.InvalidCredentials, result.Error.Code);
            Assert.AreEqual("틀림", result.Error.Message);
        }

        [Test]
        public void NonErrorBodyHasNoError()
        {
            Assert.IsNull(UnityHttpTransport.ToResult(UnityWebRequest.Result.ProtocolError, 404, null, "<html></html>").Error);
            Assert.IsNull(UnityHttpTransport.ToResult(UnityWebRequest.Result.ProtocolError, 400, null, "").Error);
            Assert.IsNull(UnityHttpTransport.ParseError("{\"error\":{}}"));
        }

        [Test]
        public void FiveHundredIsResponseButNotReached()
        {
            string body = FakeHttpTransport.ErrorBody(ApiErrorCodes.InternalError);

            HttpResult result = UnityHttpTransport.ToResult(UnityWebRequest.Result.ProtocolError, 503, null, body);

            Assert.AreEqual(HttpOutcome.Response, result.Outcome);
            Assert.IsFalse(result.Reached);
            Assert.AreEqual(ApiErrorCodes.InternalError, result.Error.Code);
        }

        [Test]
        public void TimeoutMessageIsTimeout()
        {
            HttpResult result = UnityHttpTransport.ToResult(UnityWebRequest.Result.ConnectionError, 0, "Request timeout", "");

            Assert.AreEqual(HttpOutcome.Timeout, result.Outcome);
            Assert.AreEqual("Request timeout", result.TransportMessage);
        }

        [Test]
        public void ConnectionFailureIsNetworkError()
        {
            HttpResult result = UnityHttpTransport.ToResult(UnityWebRequest.Result.ConnectionError, 0, "Cannot connect to destination host", "");

            Assert.AreEqual(HttpOutcome.NetworkError, result.Outcome);
            Assert.IsFalse(result.Reached);
            Assert.AreEqual("", result.Body);
        }

        [UnityTest]
        public IEnumerator SendsHeadersAndBodyAndReadsServerError()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                string errorBody = FakeHttpTransport.ErrorBody(ApiErrorCodes.Unauthorized, "만료");
                Task<string> received = AnswerOnceAsync(listener, "401 Unauthorized", errorBody);

                var transport = new UnityHttpTransport($"http://127.0.0.1:{Port(listener)}/");
                Task<HttpResult> sending = transport.SendAsync(
                    new HttpRequest(HttpRequest.Post, ApiPaths.Sync, "{\"logs\":[]}", "token-1", 5));

                yield return TestTasks.Wait(sending, 10);

                HttpResult result = sending.Result;
                Assert.AreEqual(HttpOutcome.Response, result.Outcome, result.TransportMessage);
                Assert.AreEqual(401, result.StatusCode);
                Assert.AreEqual(ApiErrorCodes.Unauthorized, result.Error.Code);

                string request = received.Result;
                StringAssert.StartsWith("POST " + ApiPaths.Sync + " ", request); // baseUrl 끝의 '/'가 겹치지 않는다
                StringAssert.Contains("Authorization: Bearer token-1", request);
                StringAssert.Contains("Content-Type: application/json", request);
                StringAssert.EndsWith("{\"logs\":[]}", request);
            }
            finally
            {
                listener.Stop();
            }
        }

        [UnityTest]
        public IEnumerator SilentServerTimesOut()
        {
            // 연결은 받지만(백로그) 답하지 않는다.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var transport = new UnityHttpTransport($"http://127.0.0.1:{Port(listener)}");
                Task<HttpResult> sending = transport.SendAsync(new HttpRequest(HttpRequest.Get, "/", null, null, 1));

                yield return TestTasks.Wait(sending, 10);

                Assert.AreEqual(HttpOutcome.Timeout, sending.Result.Outcome, sending.Result.TransportMessage);
            }
            finally
            {
                listener.Stop();
            }
        }

        [UnityTest]
        public IEnumerator ClosedPortIsNetworkError()
        {
            // 비어 있는 포트를 얻어 닫는다. 연결이 곧바로 거부된다.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = Port(listener);
            listener.Stop();

            var transport = new UnityHttpTransport($"http://127.0.0.1:{port}");
            Task<HttpResult> sending = transport.SendAsync(new HttpRequest(HttpRequest.Get, "/", null, null, 10));

            yield return TestTasks.Wait(sending, 15);

            Assert.AreEqual(HttpOutcome.NetworkError, sending.Result.Outcome, sending.Result.TransportMessage);
            Assert.IsNotEmpty(sending.Result.TransportMessage);
        }

        [Test]
        public void NullRequestIsCallerBug()
        {
            Task<HttpResult> sending = new UnityHttpTransport("http://127.0.0.1").SendAsync(null);

            Assert.IsTrue(sending.IsFaulted);
            Assert.IsInstanceOf<ArgumentNullException>(sending.Exception.InnerException);
        }

        private static int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

        // 요청 하나를 받아 그대로 돌려주고(검사용), 정해 둔 상태·본문으로 답한다.
        private static Task<string> AnswerOnceAsync(TcpListener listener, string status, string body) => Task.Run(async () =>
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();

            string request = await ReadRequestAsync(stream);

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string head = $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
            byte[] headBytes = Encoding.ASCII.GetBytes(head);
            await stream.WriteAsync(headBytes, 0, headBytes.Length);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
            return request;
        });

        // 헤더 끝까지 읽고, Content-Length만큼 본문을 더 읽는다.
        private static async Task<string> ReadRequestAsync(NetworkStream stream)
        {
            var buffer = new byte[8192];
            int length = 0;
            int headerEnd;
            while ((headerEnd = IndexOfHeaderEnd(buffer, length)) < 0)
                length += await ReadSomeAsync(stream, buffer, length);

            string head = Encoding.ASCII.GetString(buffer, 0, headerEnd);
            int bodyLength = ContentLength(head);
            while (length < headerEnd + 4 + bodyLength)
                length += await ReadSomeAsync(stream, buffer, length);

            return head + "\r\n\r\n" + Encoding.UTF8.GetString(buffer, headerEnd + 4, bodyLength);
        }

        private static async Task<int> ReadSomeAsync(NetworkStream stream, byte[] buffer, int offset)
        {
            int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset);
            if (read == 0) throw new InvalidOperationException("요청이 끝나기 전에 연결이 닫혔다.");
            return read;
        }

        private static int IndexOfHeaderEnd(byte[] buffer, int length)
        {
            for (int i = 0; i + 3 < length; i++)
                if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                    return i;
            return -1;
        }

        private static int ContentLength(string head)
        {
            foreach (string line in head.Split("\r\n"))
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    return int.Parse(line.Substring("Content-Length:".Length).Trim());
            return 0;
        }
    }
}
