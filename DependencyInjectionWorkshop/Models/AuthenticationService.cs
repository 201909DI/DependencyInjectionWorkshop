﻿using System;
using NLog;

namespace DependencyInjectionWorkshop.Models
{
    public interface IAuthentication
    {
        bool Verify(string accountId, string password, string otp);
    }

    public class NotificationDecorator
    {
        private AuthenticationService _authenticationService;

        public NotificationDecorator(AuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        private void Notify(string accountId)
        {
            _authenticationService._notification.Notify(accountId);
        }
    }

    public class AuthenticationService : IAuthentication
    {
        private readonly IFailedCounter _failedCounter;
        private readonly IHash _hash;
        private readonly ILogger _logger;
        private readonly INotification _notification;
        private readonly IOtpService _otpService;
        private readonly IProfile _profile;
        private readonly NotificationDecorator _notificationDecorator;

        public AuthenticationService(IFailedCounter failedCounter, IHash hash, ILogger logger,
            INotification notification, IOtpService otpService, IProfile profile)
        {
            _notificationDecorator = new NotificationDecorator(this);
            _failedCounter = failedCounter;
            _hash = hash;
            _logger = logger;
            _notification = notification;
            _otpService = otpService;
            _profile = profile;
        }

        public AuthenticationService()
        {
            _notificationDecorator = new NotificationDecorator(this);
            _profile = new ProfileDao();
            _hash = new Sha256Adapter();
            _otpService = new OtpService();
            _failedCounter = new FailedCounter();
            _notification = new SlackAdapter();
            _logger = new NLogAdapter();
        }

        public bool Verify(string accountId, string password, string otp)
        {
            if (_failedCounter.IsAccountLocked(accountId))
            {
                throw new FailedTooManyTimesException();
            }

            var currentPassword = _profile.GetPassword(accountId);

            var hashedPassword = _hash.Compute(password);

            var currentOtp = _otpService.GetCurrentOtp(accountId);

            if (currentPassword == hashedPassword && otp == currentOtp)
            {
                _failedCounter.Reset(accountId);

                return true;
            }
            else
            {
                _failedCounter.Add(accountId);

                _notificationDecorator.Notify(accountId);

                int failedCount = _failedCounter.Get(accountId);
                _logger.Info($"accountId:{accountId} failed times:{failedCount}");

                return false;
            }
        }
    }

    public class FailedTooManyTimesException : Exception
    {
    }
}