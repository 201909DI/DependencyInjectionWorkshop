﻿using System;
using System.Net.Http;
using System.Text;
using DependencyInjectionWorkshop.Repos;
using SlackAPI;

namespace DependencyInjectionWorkshop.Models
{
    public class Sha256Adapter
    {
        public string GetHashedInputPassword(string inputPassword)
        {
            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new StringBuilder();
            var crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(inputPassword));
            foreach (var theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }

            var hashedInputPassword = hash.ToString();
            return hashedInputPassword;
        }
    }


    public class OtpService
    {
        public string GetCurrentOtp(string accountId)
        {
            var response = new HttpClient() {BaseAddress = new Uri("http://joey.com/")}.PostAsJsonAsync("api/otps", accountId).Result;
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"web api error, accountId:{accountId}");
            }

            var currentOtp = response.Content.ReadAsAsync<string>().Result;
            return currentOtp;
        }
    }


    public class SlackAdapter
    {
        public void Notify(string accountId)
        {
            var slackClient = new SlackClient("my api token");
            slackClient.PostMessage(response1 => { }, "my channel", $"{accountId} try to login failed", "my bot name");
        }
    }
    
    public class FailedCounter
    {
        public void ResetFailedCounter(string accountId)
        {
            var resetResponse = new HttpClient() {BaseAddress = new Uri("http://joey.com/")}.PostAsJsonAsync("api/failedCounter/Reset", accountId).Result;
            resetResponse.EnsureSuccessStatusCode();
        }

        public void AddFailedCount(string accountId)
        {
            var addFailedCountResponse = new HttpClient() {BaseAddress = new Uri("http://joey.com/")}.PostAsJsonAsync("api/failedCounter/Add", accountId).Result;
            addFailedCountResponse.EnsureSuccessStatusCode();
        }

        public bool GetAccountIsLocked(string accountId)
        {
            var isLockedResponse = new HttpClient() {BaseAddress = new Uri("http://joey.com/")}.PostAsJsonAsync("api/failedCounter/IsLocked", accountId).Result;
            isLockedResponse.EnsureSuccessStatusCode();
            bool isLocked = isLockedResponse.Content.ReadAsAsync<bool>().Result;
            return isLocked;
        }

        public int GetFailedCount(string accountId)
        {
            var failedCountResponse =
                new HttpClient() {BaseAddress = new Uri("http://joey.com/")}.PostAsJsonAsync("api/failedCounter/GetFailedCount", accountId).Result;

            failedCountResponse.EnsureSuccessStatusCode();

            var failedCount = failedCountResponse.Content.ReadAsAsync<int>().Result;
            return failedCount;
        }
    }
    public class NLogAdapter
    {
        public void LogMessage(string message)
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info(message);
        }
    }

    public class AuthenticationService
    {
        private readonly IProfile _profile;
        private readonly Sha256Adapter _sha256Adapter;
        private readonly OtpService _otpService;
        private readonly SlackAdapter _slackAdapter;
        private readonly FailedCounter _failedCounter;
        private readonly NLogAdapter _nlogAdapter;

        public AuthenticationService(IProfile profile, Sha256Adapter sha256Adapter, OtpService otpService, SlackAdapter slackAdapter, FailedCounter failedCounter, NLogAdapter nlogAdapter)
        {
            _profile = profile;
            _sha256Adapter = sha256Adapter;
            _otpService = otpService;
            _slackAdapter = slackAdapter;
            _failedCounter = failedCounter;
            _nlogAdapter = nlogAdapter;
        }

        public AuthenticationService()
        {
            _profile = new ProfileDao();
            _sha256Adapter = new Sha256Adapter();
            _otpService = new OtpService();
            _slackAdapter = new SlackAdapter();
            _failedCounter = new FailedCounter();
            _nlogAdapter = new NLogAdapter();
        }

        public bool Verify(string accountId, string inputPassword, string otp)
        {
            // check is lock before verify
            var isLocked = _failedCounter.GetAccountIsLocked(accountId);
            if (isLocked)
            {
                throw new FailedTooManyTimesException();
            }
            
            var passwordFromDb = _profile.GetPassword(accountId);
            var hashedInputPassword = _sha256Adapter.GetHashedInputPassword(inputPassword);
            var currentOtp = _otpService.GetCurrentOtp(accountId);

            if (passwordFromDb == hashedInputPassword && otp == currentOtp)
            {
                // login success, reset failed counter
                _failedCounter.ResetFailedCounter(accountId);

                return true;
            }
            else
            {
                _failedCounter.AddFailedCount(accountId);
                LogFailedCount(accountId);
                _slackAdapter.Notify(accountId);

                return false;
            } 
        }

        private void LogFailedCount(string accountId)
        {
            var failedCount = _failedCounter.GetFailedCount(accountId);
            
            _nlogAdapter.LogMessage($"accountId:{accountId} failed times:{failedCount}");
        }
    }



    public class FailedTooManyTimesException : Exception
    {
    }
}