﻿using DependencyInjectionWorkshop.Models;
using NSubstitute;
using NUnit.Framework;

namespace DependencyInjectionWorkshopTests
{
    [TestFixture]
    public class AuthenticationServiceTests
    {
        private IProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = Substitute.For<IProfile>();
        }

        [Test]
        public void is_valid()
        {
            var hash = Substitute.For<IHash>();
            var otpService = Substitute.For<IOtpService>();
            var failedCounter = Substitute.For<IFailedCounter>();
            var notification = Substitute.For<INotification>();
            var logger = Substitute.For<ILogger>();

            var authenticationService =
                new AuthenticationService(failedCounter, hash, logger, notification, otpService, _profile);

            _profile.GetPassword("joey").Returns("my hashed password");
            hash.Compute("abc").Returns("my hashed password");
            otpService.GetCurrentOtp("joey").Returns("123456");

            var isValid = authenticationService.Verify("joey", "abc", "123456");
            Assert.IsTrue(isValid);
        }
    }
}