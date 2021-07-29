using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

using Resto.Front.Api.Data.Cheques;
using Resto.Front.Api.Data.View;
using Resto.Front.Api.UI;
using Resto.Front.Api.Extensions;
using System.Xml.Linq;
using Resto.Front.Api.Data.Orders;
using Resto.Front.Api.Data.Print;
using Resto.Front.Api.Data.Payments;
using Resto.Front.Api.Attributes.JetBrains;

namespace Resto.Front.Api.OlginkaPlugin
{
    using static PluginContext;

    internal sealed class PayButton : IDisposable
    {
        private readonly CompositeDisposable subscriptions;

        static private decimal TryGetOrderExternalSum([NotNull] Guid orderId) {
            decimal sum = 0;

            if (decimal.TryParse(Operations.TryGetOrderExternalDataByKey(orderId, "sum"), out decimal res))
            {
                sum += res;
            }

            return sum;
        }

        static private void printCafeSessionResulSum([NotNull] IPrinterRef printer, string title) 
        {
            decimal sum = Operations.GetOrders().Where(o => o.Status != OrderStatus.Deleted).Sum(o => o.ResultSum + TryGetOrderExternalSum(o.Id));
            
            Log.Info($"{DateTime.Now:g} {title} Итого наличных: {sum}");
            Operations.AddNotificationMessage($"{DateTime.Now.ToString("g")} Итого наличных: {sum}","Plugin",TimeSpan.FromSeconds(15));

            var slip = new Document
            {
                Markup = new XElement(Tags.Doc
                    , new XElement(Tags.Line, new XAttribute("symbols", "*"))
                    , new XElement(Tags.Left, string.Format("{0} ----- {1}", "Мой ресторан", "ООО \"Мое торговое предприятие\""))
                    , new XElement(Tags.Left, "ИНН")
                    , new XElement(Tags.Line, new XAttribute("symbols", "*"))
                    , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Касса: 1"), new XAttribute(Tags.Right, "Основная группа"))
                    , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Кассовая смена:"), new XAttribute(Tags.Right, "1"))
                    , new XElement(Tags.Center, "Итого наличных")
                    , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Дата"), new XAttribute(Tags.Right, DateTime.Now.ToString("g")))
                    , new XElement(Tags.Line)
                    , new XElement(Tags.Left, sum.ToString())
                    , new XElement(Tags.Line)

                )

            };

            Operations.Print(printer,slip);
        }

        public PayButton()
        {
            subscriptions = new CompositeDisposable
            {
                Notifications.CafeSessionOpening.Subscribe(x => printCafeSessionResulSum(Operations.TryGetBillPrinter(), "Открытие смены")),

                Notifications.CafeSessionClosing.Subscribe(x => printCafeSessionResulSum(Operations.TryGetBillPrinter(), "Закрытие смены")),

                Operations.AddButtonToPluginsMenu("Итого нала", x => printCafeSessionResulSum(Operations.TryGetBillPrinter(), "Ручная проверка")),

                Operations.AddButtonToOrderEditScreen("НАЛ", x => {

                    x.os.Print(Operations.TryGetReceiptChequePrinter(), new Document { Markup = new XElement(Tags.Doc
                        , new XElement(Tags.Line, new XAttribute ("symbols",  "*"))
                        , new XElement(Tags.Left, string.Format("{0} ----- {1}","Мой ресторан", "ООО \"Мое торговое предприятие\"") )
                        , new XElement(Tags.Left,"ИНН")
                        , new XElement(Tags.Line, new XAttribute ("symbols",  "*"))
                        , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Касса: 1"), new XAttribute(Tags.Right, "Основная группа"))
                        , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Кассовая смена:"), new XAttribute(Tags.Right, "1"))
                        , new XElement(Tags.Center,"Квитанция об оплате заказа")
                        , new XElement(Tags.Pair, new XAttribute (Tags.Left,"Дата"), new XAttribute (Tags.Right,  DateTime.Now.ToString("g")) )
                        , new XElement(Tags.Pair, new XAttribute (Tags.Left,"Кассир: ТВ"), new XAttribute (Tags.Right,  "Заказ № 2" ) )
                        , new XElement(Tags.Left, "Официант: ТВ" )
                        , new XElement(Tags.Left, string.Format("Зал: {0} Стол {1} Гостей {2}", "Бар", "3", "1"))
                        , new XElement(Tags.Table
                            , new XElement(Tags.Columns
                                , new XElement(Tags.Column, new XAttribute("formatter", "split") )
                                , new XElement(Tags.Column, new XAttribute("align", "right"), new XAttribute("autowidth", ""))
                                , new XElement(Tags.Column, new XAttribute("align", "right"), new XAttribute("width", "10")))
                            , new XElement( Tags.Cells
                                , new XElement(Tags.LineCell)
                                , new XElement(Tags.TextCell, "Наименование")
                                , new XElement(Tags.TextCell, "К-во")
                                , new XElement(Tags.TextCell, "Сумма")
                                , new XElement(Tags.LineCell)
                                , x.order.Items.OfType<IOrderProductItem>().Where(y => !y.Deleted).SelectMany(y => new XElement[] {
                                    new XElement(Tags.TextCell, y.Product?.Name),
                                    new XElement(Tags.TextCell, y.Amount),
                                    new XElement(Tags.TextCell, y.ResultSum.ToString())
                                })))
                        , new XElement(Tags.Line)
                        , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Итого к оплате:"), new XAttribute(Tags.Right, x.order.ResultSum))
                        , new XElement(Tags.Line)
                        , new XElement(Tags.Pair, new XAttribute(Tags.Left, "Наличные"), new XAttribute(Tags.Right, x.order.ResultSum))
                        , new XElement(Tags.Center, string.Format("ВСЕ СУММЫ В {0}", "РУБЛЯХ"))
                        , new XElement(Tags.NewParagraph)
                        , new XElement(Tags.Center,"СПАСИБО! ЖДЕМ ВАС СНОВА!")
                        , new XElement(Tags.NewParagraph)
                        , new XElement(Tags.Line)
                        , new XElement(Tags.Center,"ПОДПИСЬ")
                        ) 
                    });

                    var editSession = Operations.CreateEditSession();

                    decimal sum = 0;

                    foreach (var item in x.order.Items.OfType<IOrderProductItem>().Where(i => !i.PrintTime.HasValue && !i.Deleted) ) 
                    {
                        sum += item.ResultSum;
                        editSession.DeleteOrderItem (x.order,item);
                    }

                    Log.Info($"{DateTime.Now:g} Оплата на сумму: {sum}");
                    
                    sum += TryGetOrderExternalSum(x.order.Id);
                    editSession.AddOrderExternalData("sum",sum.ToString(),x.order);

                    x.os.SubmitChanges(x.os.GetCredentials(), editSession);
                }),
            };
        }

        public void Dispose()
        {
            subscriptions.Dispose();
        }
    }
}
