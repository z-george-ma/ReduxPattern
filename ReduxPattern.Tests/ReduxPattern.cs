using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Net;

namespace ReduxPattern.Tests
{
    [TestClass]
    public class ReduxPatternTests
    {
        [TestMethod]
        public async Task Example()
        {
            var store = Substitute.For<IStore<string>>();

            store.GetState().Returns(async x =>
            {
                // Get data from Redis
                await Task.Delay(10);

                return "I have $10 in my pocket";
            });

            store.SaveState(Arg.Any<string>(), Arg.Any<string>()).Returns(async x =>
            {
                // Save data into Redis
                await Task.Delay(10);
            });

            var result = await "Buy an ice cream"
                    .Use(store)
                    .Reduce((string state, string action) =>
                    {
                        Assert.AreEqual("I have $10 in my pocket", state);
                        Assert.AreEqual("Buy an ice cream", action);
                        return "Now I have $5";
                    })
                    .Effect(async (string prevState, string newState, string action) =>
                    {
                        // Persist into database
                        await Task.Delay(100);

                        return HttpStatusCode.OK;
                    });

            Assert.AreEqual(HttpStatusCode.OK, result);

            // Redis should have correct state
            await store.Received().SaveState(
                "Now I have $5", // current state
                "I have $10 in my pocket" // previous state
            );
        }

        [TestMethod]
        public async Task Example_With_Exceptions()
        {
            var store = Substitute.For<IStore<string>>();

            store.GetState().Returns(async x =>
            {
                // Get data from Redis
                await Task.Delay(10);

                return "I have $10 in my pocket";
            });

            store.SaveState(Arg.Any<string>(), Arg.Any<string>()).Returns(async x =>
            {
                // Save data into Redis
                await Task.Delay(10);
            });

            string initialState = null, updatedState = null;

            var result = await "Buy an ice cream"
                    .Use(store)
                    .Reduce((string state, string action) =>
                    {
                        Assert.AreEqual("I have $10 in my pocket", state);
                        Assert.AreEqual("Buy an ice cream", action);
                        return "Now I have $5";
                    })
                    .Effect<string, string, HttpStatusCode>(async (string prevState, string newState, string action) =>
                    {
                        initialState = prevState;
                        updatedState = newState;

                        // Persist into database
                        await Task.Delay(100);

                        throw new Exception("Failed to persist into database");
                    })
                    .Catch(async (Exception e) =>
                    {
                        Assert.AreEqual("Failed to persist into database", e.Message);

                        // roll back state in Redis
                        await store.SaveState(initialState, updatedState);

                        return HttpStatusCode.InternalServerError;
                    });

            Assert.AreEqual(HttpStatusCode.InternalServerError, result);

            // Redis should have correct state
            await store.Received().SaveState(
                "I have $10 in my pocket", // previous state
                "Now I have $5" // current state
            );
        }
    }
}
