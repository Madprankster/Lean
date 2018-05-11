/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using DynamicInterop;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm for adding and removing option chains
    /// </summary>
    public class AddRemoveOptionChainRegressionAlgorithm : QCAlgorithm
    {
        private Option TwxOptionChain;
        private Option AaplOptionChain;
        private Symbol TwxOptionChainSymbol;
        private Symbol AaplOptionChainSymbol;

        private bool removedTwx = false;
        private bool removedAapl = false;

        public override void Initialize()
        {
            SetStartDate(2014, 06, 05);
            SetEndDate(2014, 06, 09);
            SetCash(100000);

            TwxOptionChain = AddOption("TWX");
            TwxOptionChainSymbol = TwxOptionChain.Symbol;
            AaplOptionChainSymbol = QuantConnect.Symbol.Create("AAPL", SecurityType.Option, Market.USA, "?AAPL");

            TwxOptionChain.SetFilter(filter =>
            {
                Symbol selectedContract = null;
                return filter
                    // limit options contracts to a maximum of 180 days in the future
                    .Expiration(TimeSpan.Zero, TimeSpan.FromDays(180))
                    .Contracts(contracts =>
                    {
                        if (selectedContract == null)
                        {
                            // select the latest expiring, closest to the money put contract
                            selectedContract = contracts.Where(x => x.ID.OptionRight == OptionRight.Put)
                                .OrderByDescending(x => x.ID.Date)
                                .ThenBy(x => Math.Abs(filter.Underlying.Price - x.ID.StrikePrice))
                                .FirstOrDefault();
                        }

                        return new[] {selectedContract};
                    });
            });

            // add AAPL options at EOD 06.05.2014, to receive on the 6th
            Schedule.On(DateRules.On(2014, 06, 05), TimeRules.At(TimeSpan.FromHours(15)), () =>
            {
                AaplOptionChain = AddOption("AAPL");

                AaplOptionChain.SetFilter(filter =>
                {
                    Symbol selectedContract = null;
                    return filter
                        // limit options contracts to a maximum of 180 days in the future
                        .Expiration(TimeSpan.Zero, TimeSpan.FromDays(180))
                        .Contracts(contracts =>
                        {
                            if (selectedContract == null)
                            {
                                // using security.price since there's a split bug in filter.underlying.price
                                selectedContract = contracts.Where(x => x.ID.OptionRight == OptionRight.Put)
                                    .OrderByDescending(x => x.ID.Date)
                                    .ThenBy(x => Math.Abs(Securities["AAPL"].Price - x.ID.StrikePrice))
                                    .FirstOrDefault();
                            }

                            return new[] {selectedContract};
                        });
                });
            });
        }

        public override void OnData(Slice data)
        {
            // logging for debugging symbols that come in at start of day
            if (Time.TimeOfDay.Hours == 9 && (Time.TimeOfDay.Minutes == 31))
            {
                var symbols = string.Join(", ", data.Keys.Select(symbol => symbol.SecurityType == SecurityType.Option
                        ? SymbolRepresentation.GenerateOptionTickerOSI(symbol)
                        : symbol.ToString())
                    .OrderBy(k => k));
                Console.WriteLine($"{Time:o}:: OnData(Slice):: {symbols}");

                var hasTwx = data.Any(kvp => data.Keys.Any(oc => oc.HasUnderlying && oc.Underlying == TwxOptionChainSymbol.Underlying));
                var hasAapl = data.Any(kvp => data.Keys.Any(oc => oc.HasUnderlying && oc.Underlying == AaplOptionChainSymbol.Underlying));
                Console.WriteLine($">>{Time:o}:: SLICE SYMBOLS>> " + string.Join(", ", data.Keys));
                Console.WriteLine($">>{Time:o}:: SLICE SYMBOLS>> HAS TWX: {hasTwx}  HAS AAPL: {hasAapl}");
            }

            // EOD 06.05.2014 remove TWX
            if (!removedTwx && Time.Date == new DateTime(2014, 06, 05) && Time.TimeOfDay.TotalHours == 15)
            {
                Console.WriteLine($">>{Time:o}:: Remove TWX Chain");
                RemoveSecurity(TwxOptionChainSymbol);
                removedTwx = true;
            }

            // EOS 06.06.2014
            if (!removedAapl && Time.Date == new DateTime(2014, 06, 06) && Time.TimeOfDay.TotalHours == 15)
            {
                Console.WriteLine($">>{Time:o}:: Remove AAPL Chain");
                RemoveSecurity(AaplOptionChainSymbol);
                removedAapl = true;
            }
        }

        public override void OnOrderEvent(OrderEvent fill)
        {
            Console.WriteLine($"{Time:o}:: {fill}");
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            Console.WriteLine($"{Time:o}:: {changes}");
            foreach (var added in changes.AddedSecurities)
            {
                if (added.Symbol.HasUnderlying)
                {
                    MarketOrder(added.Symbol, 1);
                }
            }
            foreach (var removed in changes.RemovedSecurities)
            {
                if (removed.Symbol.HasUnderlying)
                {
                    MarketOrder(removed.Symbol, 1);
                }
            }
        }
    }
}