using System;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;

namespace AlMadinaERP.Services
{
    public class PrintService : IPrintService
    {
        private Image? TryGetLogoImage(double width = 120)
        {
            try
            {
                string[] paths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "almadina_logo.jpeg"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "almadina_logo.jpeg"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "almadina logo.jpeg")
                };

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        return new Image
                        {
                            Source = bitmap,
                            Width = width,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 8)
                        };
                    }
                }
            }
            catch { }
            return null;
        }

        public void PrintThermalReceipt(SaleInvoice invoice, CompanySetting company)
        {
            if (invoice == null) return;
            company ??= new CompanySetting();
            invoice.Items ??= new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    bool isReturn = invoice.Type == InvoiceType.SaleReturn || (invoice.InvoiceNumber?.StartsWith("INV-RET") == true);
                    decimal cashBack = Math.Max(0m, invoice.PaidAmount - invoice.TotalAmount);

                    var doc = new FlowDocument
                    {
                        PageWidth = 280, // 80mm thermal paper printable width
                        ColumnWidth = 280,
                        PagePadding = new Thickness(4),
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11
                    };

                    // 1. Center Logo
                    var logo = TryGetLogoImage(110);
                    if (logo != null) doc.Blocks.Add(new BlockUIContainer(logo));

                    // 2. Company Name
                    var companyNameStr = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName.ToUpper();
                    var companyPar = new Paragraph(new Run(companyNameStr))
                    {
                        TextAlignment = TextAlignment.Center,
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    doc.Blocks.Add(companyPar);

                    // 3. Telephone Number (Exact required POS phone)
                    var companyPhone = string.IsNullOrWhiteSpace(company.Phone) ? "03351279963" : company.Phone;
                    var phonePar = new Paragraph(new Run("Tel: " + companyPhone))
                    {
                        TextAlignment = TextAlignment.Center,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    doc.Blocks.Add(phonePar);

                    // 4. Black Filled Title Bar: SALE RECEIPT
                    var titleBorder = new Border
                    {
                        Background = System.Windows.Media.Brushes.Black,
                        Padding = new Thickness(6, 4, 6, 4),
                        Margin = new Thickness(0, 4, 0, 8),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var titleText = new TextBlock
                    {
                        Text = isReturn ? "SALE RETURN RECEIPT" : "SALE RECEIPT",
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    };
                    titleBorder.Child = titleText;
                    doc.Blocks.Add(new BlockUIContainer(titleBorder));

                    // 5. Customer Information Grid / Details (No Sales Person, Operator AMBM)
                    var detailsPar = new Paragraph();
                    detailsPar.Margin = new Thickness(0, 2, 0, 6);
                    detailsPar.LineHeight = 16;

                    detailsPar.Inlines.Add(new Run("Receipt No.     ") { FontSize = 11 });
                    detailsPar.Inlines.Add(new Run($"{invoice.InvoiceNumber ?? ""}\n") { FontSize = 11, FontWeight = FontWeights.Bold });

                    detailsPar.Inlines.Add(new Run("Date  ") { FontSize = 11 });
                    detailsPar.Inlines.Add(new Run($"{invoice.Date:dd-MM-yyyy}   ") { FontSize = 11, FontWeight = FontWeights.Bold });
                    detailsPar.Inlines.Add(new Run("Time  ") { FontSize = 11 });
                    detailsPar.Inlines.Add(new Run($"{invoice.Date:hh:mm:ss tt}\n") { FontSize = 11, FontWeight = FontWeights.Bold });

                    detailsPar.Inlines.Add(new Run("Operator Name: ") { FontSize = 11 });
                    detailsPar.Inlines.Add(new Run($"AMBM\n") { FontSize = 11, FontWeight = FontWeights.Bold });

                    detailsPar.Inlines.Add(new Run("Customer Name: ") { FontSize = 11 });
                    detailsPar.Inlines.Add(new Run($"{(string.IsNullOrWhiteSpace(invoice.CustomerName) ? "WALK-IN CUSTOMER" : invoice.CustomerName.ToUpper())}\n") { FontSize = 11, FontWeight = FontWeights.Bold });

                    detailsPar.Inlines.Add(new Run("Payment Type: ") { FontSize = 11 });
                    detailsPar.Inlines.Add(new Run($"{(string.IsNullOrWhiteSpace(invoice.PaymentMethod) ? "CASH" : invoice.PaymentMethod.ToUpper())}\n") { FontSize = 11, FontWeight = FontWeights.Bold });

                    doc.Blocks.Add(detailsPar);

                    // 6. Product Table: Description | Qty | Price/Unit | Total
                    var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 6) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) }); // Description
                    table.Columns.Add(new TableColumn { Width = new GridLength(45) });  // Qty
                    table.Columns.Add(new TableColumn { Width = new GridLength(70) });  // Price/Unit
                    table.Columns.Add(new TableColumn { Width = new GridLength(55) });  // Total

                    var rowGroup = new TableRowGroup();
                    
                    // Table Header Row
                    var headerRow = new TableRow();
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Description")) { FontWeight = FontWeights.Bold, FontSize = 11 }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Qty")) { FontWeight = FontWeights.Bold, FontSize = 11, TextAlignment = TextAlignment.Right }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Price/Unit")) { FontWeight = FontWeights.Bold, FontSize = 11, TextAlignment = TextAlignment.Right }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Total")) { FontWeight = FontWeights.Bold, FontSize = 11, TextAlignment = TextAlignment.Right }));
                    rowGroup.Rows.Add(headerRow);

                    // Items Top Divider Line
                    doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0, 2, 0, 4) });

                    int itemCount = 0;
                    decimal totalQty = 0m;
                    foreach (var item in invoice.Items)
                    {
                        if (item == null) continue;
                        itemCount++;
                        totalQty += item.Quantity;

                        string unitStr = string.IsNullOrWhiteSpace(item.UnitName) ? "Pcs" : item.UnitName;
                        string itemNameDisplay = item.ItemName ?? "";
                        string rateDisplay = $"{item.Rate:N0}/{unitStr}";
                        if (item.IsSpecialLengthItem && item.LengthFeet > 0)
                        {
                            itemNameDisplay += $" ({item.LengthFeet:0.##} ft)";
                            rateDisplay = $"{item.RatePerFoot:N0}/ft";
                        }

                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run(itemNameDisplay)) { FontSize = 10, FontWeight = FontWeights.Normal }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.Quantity:0.##}")) { FontSize = 10, TextAlignment = TextAlignment.Right }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(rateDisplay)) { FontSize = 9, TextAlignment = TextAlignment.Right }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.TotalPrice:N0}")) { FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                        rowGroup.Rows.Add(row);

                        // Live Received / Not Received Status Line directly under item
                        var statusRow = new TableRow();
                        var statusText = item.IsReceived ? "✓ RECEIVED" : "✗ NOT RECEIVED";
                        var statusBrush = item.IsReceived ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.DarkRed;
                        
                        var statusCell = new TableCell(new Paragraph(new Run(statusText)) { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = statusBrush });
                        statusCell.ColumnSpan = 4;
                        statusRow.Cells.Add(statusCell);
                        rowGroup.Rows.Add(statusRow);
                    }
                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    // Divider after products table
                    doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0, 2, 0, 4) });

                    // Item Count & Total Qty Summary
                    var countPar = new Paragraph();
                    countPar.Inlines.Add(new Run($"Item(s)  {itemCount}") { FontWeight = FontWeights.Bold, FontSize = 11 });
                    countPar.Inlines.Add(new Run($"                       Total Qty  {totalQty:0.##}\n") { FontWeight = FontWeights.Bold, FontSize = 11 });
                    doc.Blocks.Add(countPar);

                    doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0, 2, 0, 4) });

                    // 7. Totals Section
                    var totalsPar = new Paragraph();
                    totalsPar.Margin = new Thickness(0, 4, 0, 4);

                    totalsPar.Inlines.Add(new Run("Gross Total") { FontWeight = FontWeights.Bold, FontSize = 12 });
                    totalsPar.Inlines.Add(new Run($"                                    {invoice.Subtotal:N2}\n") { FontWeight = FontWeights.Bold, FontSize = 12 });

                    if (invoice.VehicleCharges > 0)
                    {
                        totalsPar.Inlines.Add(new Run("Vehicle Charges") { FontWeight = FontWeights.Bold, FontSize = 12 });
                        totalsPar.Inlines.Add(new Run($"                                {invoice.VehicleCharges:N2}\n") { FontWeight = FontWeights.Bold, FontSize = 12 });
                    }

                    if (invoice.ExtraCharges > 0)
                    {
                        totalsPar.Inlines.Add(new Run("Extra Expenses") { FontWeight = FontWeights.Bold, FontSize = 12 });
                        totalsPar.Inlines.Add(new Run($"                                 {invoice.ExtraCharges:N2}\n") { FontWeight = FontWeights.Bold, FontSize = 12 });
                    }

                    totalsPar.Inlines.Add(new Run("Discount") { FontWeight = FontWeights.Bold, FontSize = 12 });
                    totalsPar.Inlines.Add(new Run($"                                       {invoice.DiscountAmount:N2}\n") { FontWeight = FontWeights.Bold, FontSize = 12 });

                    doc.Blocks.Add(totalsPar);
                    doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0, 2, 0, 4) });

                    // NET TOTAL PKR - LARGEST AND BOLDEST TEXT
                    var netTotalPar = new Paragraph();
                    netTotalPar.Margin = new Thickness(0, 4, 0, 4);
                    netTotalPar.Inlines.Add(new Run("NET TOTAL PKR        ") { FontSize = 16, FontWeight = FontWeights.Bold });
                    netTotalPar.Inlines.Add(new Run($"{invoice.TotalAmount:N2}") { FontSize = 20, FontWeight = FontWeights.Bold });
                    doc.Blocks.Add(netTotalPar);

                    doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0, 2, 0, 4) });

                    // Received, Remaining Amount & Cash Back
                    decimal remainingBal = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount - invoice.AdvanceUsed);

                    var paidPar = new Paragraph();
                    paidPar.Margin = new Thickness(0, 4, 0, 4);
                    paidPar.Inlines.Add(new Run("Amount Received PKR") { FontSize = 11 });
                    paidPar.Inlines.Add(new Run($"                             {invoice.PaidAmount:N2}\n") { FontSize = 11 });

                    if (invoice.AdvanceUsed > 0)
                    {
                        paidPar.Inlines.Add(new Run("Advance Used PKR") { FontSize = 11 });
                        paidPar.Inlines.Add(new Run($"                            {invoice.AdvanceUsed:N2}\n") { FontSize = 11 });
                    }

                    paidPar.Inlines.Add(new Run("REMAINING AMOUNT PKR") { FontSize = 12, FontWeight = FontWeights.Bold });
                    paidPar.Inlines.Add(new Run($"                   {remainingBal:N2}\n") { FontSize = 13, FontWeight = FontWeights.Bold });

                    if (cashBack > 0)
                    {
                        paidPar.Inlines.Add(new Run("Cash Back PKR") { FontSize = 11 });
                        paidPar.Inlines.Add(new Run($"                               {cashBack:N2}\n") { FontSize = 11 });
                    }
                    doc.Blocks.Add(paidPar);

                    // 8. Footer Section
                    var visitPar = new Paragraph(new Run("*Thanks For Your Visit*"))
                    {
                        TextAlignment = TextAlignment.Center,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Margin = new Thickness(0, 8, 0, 2)
                    };
                    doc.Blocks.Add(visitPar);

                    if (invoice.DiscountAmount > 0)
                    {
                        var savedPar = new Paragraph(new Run($"You Saved Rs. {invoice.DiscountAmount:N0}"))
                        {
                            TextAlignment = TextAlignment.Center,
                            FontWeight = FontWeights.Bold,
                            FontSize = 11,
                            Margin = new Thickness(0, 0, 0, 4)
                        };
                        doc.Blocks.Add(savedPar);
                    }

                    doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 4) });

                    // Developer Credit Footer (Exact user required phrasing)
                    var devCreditPar = new Paragraph(new Run("Software By: Roonjha Developers - 03152914836"))
                    {
                        TextAlignment = TextAlignment.Center,
                        FontFamily = new FontFamily("Arial"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 13
                    };
                    doc.Blocks.Add(devCreditPar);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Thermal Receipt {invoice.InvoiceNumber ?? ""}");
                }
            });
        }

        public void PrintA4SaleInvoice(SaleInvoice invoice, CompanySetting company)
        {
            if (invoice == null) return;
            company ??= new CompanySetting();
            invoice.Items ??= new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    bool isReturn = invoice.Type == InvoiceType.SaleReturn || (invoice.InvoiceNumber?.StartsWith("INV-RET") == true);
                    decimal cashBack = Math.Max(0m, invoice.PaidAmount - invoice.TotalAmount);

                    var doc = new FlowDocument
                    {
                        PageWidth = 793,
                        PageHeight = 1122,
                        PagePadding = new Thickness(35),
                        ColumnWidth = 723,
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11
                    };

                    // =========================================================================
                    // 1. TOP HEADER BANNER (DARK BLUE #0B2A5A) MATCHING USER BANNER DESIGN
                    // =========================================================================
                    var headerBanner = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 14, 16, 14),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    var mainGrid = new Grid();
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Section (Logo + Big Calligraphy Header)
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // Spacer
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Section (Left Contacts | Middle Urdu Box | Right Contacts)

                    // TOP SECTION: LOGO (LEFT) & MAIN CALLIGRAPHY HEADING (RIGHT)
                    var topGrid = new Grid();
                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Left Logo Box
                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right Calligraphy Heading

                    // Left White Logo Box
                    var logoBox = new Border
                    {
                        Background = System.Windows.Media.Brushes.White,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(6),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 150
                    };
                    var logoStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    var logoImg = TryGetLogoImage(40);
                    if (logoImg != null)
                    {
                        logoImg.HorizontalAlignment = HorizontalAlignment.Center;
                        logoStack.Children.Add(logoImg);
                    }
                    else
                    {
                        logoStack.Children.Add(new TextBlock
                        {
                            Text = "🏛️ المدینہ",
                            FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                            TextAlignment = TextAlignment.Center
                        });
                    }
                    var logoBar = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 2, 4, 2),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    logoBar.Child = new TextBlock
                    {
                        Text = "بلڈنگ میٹریل اتھل",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center
                    };
                    logoStack.Children.Add(logoBar);
                    logoBox.Child = logoStack;
                    Grid.SetColumn(logoBox, 0);
                    topGrid.Children.Add(logoBox);

                    // Right Big Calligraphy Heading in White
                    var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                    titleStack.Children.Add(new TextBlock
                    {
                        Text = "المدینہ کنکریٹ بلاک ورکس اینڈ بلڈنگ میٹریل",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 23,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right
                    });
                    Grid.SetColumn(titleStack, 1);
                    topGrid.Children.Add(titleStack);

                    Grid.SetRow(topGrid, 0);
                    mainGrid.Children.Add(topGrid);

                    // BOTTOM SECTION: LEFT CONTACTS | MIDDLE URDU BOX | RIGHT CONTACTS
                    var bottomGrid = new Grid();
                    bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) }); // Left Contacts
                    bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Middle Urdu Box
                    bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); // Right Contacts

                    // Left Side Contacts:
                    // 0333-7970848   ایم اقبال
                    // 0335-1279963   ایم اکرم
                    var leftContactStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };

                    var contactLine1 = new TextBlock { Margin = new Thickness(0, 0, 0, 2) };
                    contactLine1.Inlines.Add(new Run("0333-7970848  ") { FontFamily = new FontFamily("Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    contactLine1.Inlines.Add(new Run("ایم اقبال") { FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    leftContactStack.Children.Add(contactLine1);

                    var contactLine2 = new TextBlock();
                    contactLine2.Inlines.Add(new Run("0335-1279963  ") { FontFamily = new FontFamily("Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    contactLine2.Inlines.Add(new Run("ایم اکرم") { FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    leftContactStack.Children.Add(contactLine2);

                    Grid.SetColumn(leftContactStack, 0);
                    bottomGrid.Children.Add(leftContactStack);

                    // Middle Urdu Description Box with White Border
                    var middleBox = new Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.White,
                        BorderThickness = new Thickness(1.2),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(8, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    var middleStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    middleStack.Children.Add(new TextBlock
                    {
                        Text = "ہمارے ہاں ہر سائز کا بلاک، سیمنٹ، بجری",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center
                    });
                    middleStack.Children.Add(new TextBlock
                    {
                        Text = "ریتی، روڑی، کرش اور سریا دستیاب ہے",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                    middleBox.Child = middleStack;
                    Grid.SetColumn(middleBox, 1);
                    bottomGrid.Children.Add(middleBox);

                    // Right Side Contacts:
                    // ایم حسین
                    // 0333-7980848
                    // 0345-3799500
                    var rightContactStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                    rightContactStack.Children.Add(new TextBlock
                    {
                        Text = "ایم حسین",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right
                    });
                    rightContactStack.Children.Add(new TextBlock
                    {
                        Text = "0333-7980848",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 1, 0, 0)
                    });
                    rightContactStack.Children.Add(new TextBlock
                    {
                        Text = "0345-3799500",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 1, 0, 0)
                    });
                    Grid.SetColumn(rightContactStack, 2);
                    bottomGrid.Children.Add(rightContactStack);

                    Grid.SetRow(bottomGrid, 2);
                    mainGrid.Children.Add(bottomGrid);

                    headerBanner.Child = mainGrid;
                    doc.Blocks.Add(new BlockUIContainer(headerBanner));

                    // INVOICE TITLE BANNER: "SALE INVOICE" / "SALE RETURN" BELOW HEADER
                    var titleBanner = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(0, 4, 0, 4),
                        Margin = new Thickness(0, 2, 0, 12),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var titleBannerText = new TextBlock
                    {
                        Text = isReturn ? "SALE RETURN" : "SALE INVOICE",
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    };
                    titleBanner.Child = titleBannerText;
                    doc.Blocks.Add(new BlockUIContainer(titleBanner));

                    // 2. METADATA ROW MATCHING IMAGE 2 EXACTLY (4 COLUMNS FULL WIDTH)
                    var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Customer Name
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Invoice No
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Date
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // Payment Terms

                    var metaGroup = new TableRowGroup();
                    var metaRow = new TableRow();

                    metaRow.Cells.Add(new TableCell(new Paragraph()
                    {
                        Inlines =
                        {
                            new Run("CUSTOMER NAME\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray },
                            new Run(string.IsNullOrWhiteSpace(invoice.DisplayCustomerName) ? "Walk-in Customer" : invoice.DisplayCustomerName) { FontSize = 12, FontWeight = FontWeights.Bold }
                        }
                    }));

                    metaRow.Cells.Add(new TableCell(new Paragraph()
                    {
                        Inlines =
                        {
                            new Run(isReturn ? "RETURN NO\n" : "INVOICE NO\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray },
                            new Run(invoice.InvoiceNumber ?? "") { FontSize = 12, FontWeight = FontWeights.Bold }
                        }
                    }));

                    metaRow.Cells.Add(new TableCell(new Paragraph()
                    {
                        Inlines =
                        {
                            new Run("DATE\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray },
                            new Run($"{invoice.Date:dd/MM/yyyy}") { FontSize = 12, FontWeight = FontWeights.Bold }
                        }
                    }));

                    var methodPill = new Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.RoyalBlue,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 3, 10, 3),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = System.Windows.Media.Brushes.LightSkyBlue
                    };
                    methodPill.Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(invoice.PaymentMethod) ? "CREDIT" : invoice.PaymentMethod.ToUpper(),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.MidnightBlue
                    };

                    var methodCell = new TableCell();
                    var methodPar = new Paragraph(new Run("PAYMENT TERMS\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray });
                    methodCell.Blocks.Add(methodPar);
                    methodCell.Blocks.Add(new BlockUIContainer(methodPill));
                    metaRow.Cells.Add(methodCell);

                    metaGroup.Rows.Add(metaRow);
                    metaTable.RowGroups.Add(metaGroup);
                    doc.Blocks.Add(metaTable);

                    // 3. PRODUCTS TABLE WITH FULL STAR WIDTHS (EXACT IMAGE 2 COLUMNS)
                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = System.Windows.Media.Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(0.4, GridUnitType.Star) }); // #
                    table.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) }); // ITEM DESCRIPTION
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // DELIVERY STATUS
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // ORDERED
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // DELIVERED
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // PENDING
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // RATE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) }); // TOTAL

                    var rowGroup = new TableRowGroup();
                    var headerRow = new TableRow { Background = System.Windows.Media.Brushes.WhiteSmoke };

                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("#")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6) }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("ITEM DESCRIPTION")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6) }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("DELIVERY STATUS")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6) }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("ORDERED")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Center }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("DELIVERED")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Center }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("PENDING")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Center }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("RATE")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Right }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Right }));
                    rowGroup.Rows.Add(headerRow);

                    int idx = 1;
                    foreach (var item in invoice.Items)
                    {
                        if (item == null) continue;

                        string itemNameDisplay = item.ItemName ?? "";
                        string rateDisplay = $"PKR {item.Rate:N0}";
                        if (item.IsSpecialLengthItem && item.LengthFeet > 0)
                        {
                            itemNameDisplay += $" ({item.LengthFeet:0.##} ft @ PKR {item.RatePerFoot:N0}/ft)";
                            rateDisplay = $"PKR {item.RatePerFoot:N0}/ft";
                        }

                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run(idx.ToString())) { FontSize = 10, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(itemNameDisplay)) { FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 5, 4, 5) }));

                        var statusTxt = item.IsReceived ? "✓ RECEIVED" : "✗ NOT RECEIVED";
                        var statusClr = item.IsReceived ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkRed;
                        row.Cells.Add(new TableCell(new Paragraph(new Run(statusTxt)) { FontSize = 9, Foreground = statusClr, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 5, 4, 5) }));

                        var deliveredQtyStr = item.IsReceived ? $"{item.Quantity:0.##}" : "0";
                        var pendingQtyStr = item.IsReceived ? "—" : $"{item.Quantity:0.##}";

                        row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.Quantity:0.##}")) { FontSize = 10, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(deliveredQtyStr)) { FontSize = 10, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(pendingQtyStr)) { FontSize = 10, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(rateDisplay)) { FontSize = 10, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"PKR {item.TotalPrice:N0}")) { FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                        rowGroup.Rows.Add(row);
                        idx++;
                    }
                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    // 4. LOWER SUMMARY: TERMS (LEFT) & TOTALS (RIGHT) MATCHING IMAGE 2
                    var summaryTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 14) };
                    summaryTable.Columns.Add(new TableColumn { Width = new GridLength(4.5, GridUnitType.Star) }); // Notes/Terms
                    summaryTable.Columns.Add(new TableColumn { Width = new GridLength(2.7, GridUnitType.Star) }); // Totals

                    var sumRowGroup = new TableRowGroup();
                    var sumRow = new TableRow();

                    // LEFT CELL: URDU TERMS & CONDITIONS
                    var termsPar = new Paragraph();
                    termsPar.Inlines.Add(new Run("Notes / Terms (شرائط و ضوابط):\n") { FontWeight = FontWeights.Bold, FontSize = 10 });
                    termsPar.Inlines.Add(new Run("1. مال واپس یا تبدیل ہوسکتا ہے، بشرطیکہ اصل رسید ساتھ ہو۔\n") { FontSize = 9, FontWeight = FontWeights.Bold });
                    termsPar.Inlines.Add(new Run("2. براہ مہربانی ڈلیوری کے وقت سامان کی گنتی اور کوالٹی چیک کرلیں، بعد میں کوئی شکایت قابل قبول نہیں ہوگی۔\n") { FontSize = 9 });
                    sumRow.Cells.Add(new TableCell(termsPar));

                    // RIGHT CELL: TOTALS SUMMARY
                    var totalsPar = new Paragraph();
                    totalsPar.TextAlignment = TextAlignment.Right;
                    totalsPar.Inlines.Add(new Run($"Gross Total:   PKR {invoice.Subtotal:N2}\n") { FontSize = 10 });
                    if (invoice.VehicleCharges > 0)
                        totalsPar.Inlines.Add(new Run($"Vehicle Charges:   PKR {invoice.VehicleCharges:N2}\n") { FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.DarkSlateBlue });
                    if (invoice.ExtraCharges > 0)
                        totalsPar.Inlines.Add(new Run($"Extra Expenses:   PKR {invoice.ExtraCharges:N2}\n") { FontSize = 10 });
                    if (invoice.AdditionalDiscount > 0 || invoice.DiscountAmount > 0)
                        totalsPar.Inlines.Add(new Run($"Discount (-):   PKR {(invoice.AdditionalDiscount + invoice.DiscountAmount):N2}\n") { FontSize = 10 });
                    totalsPar.Inlines.Add(new Run($"NET TOTAL:   PKR {invoice.TotalAmount:N2}\n") { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.MidnightBlue });
                    decimal a4RemainingBal = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);
                    decimal a4CashBack = Math.Max(0m, invoice.PaidAmount - invoice.TotalAmount);

                    totalsPar.Inlines.Add(new Run($"Amount Paid:   PKR {invoice.PaidAmount:N2}\n") { FontSize = 10 });

                    if (a4RemainingBal > 0)
                    {
                        totalsPar.Inlines.Add(new Run($"Remaining Balance:   PKR {a4RemainingBal:N2}\n") { FontSize = 11, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkRed });
                    }
                    else
                    {
                        totalsPar.Inlines.Add(new Run($"Cash Back:   PKR {a4CashBack:N2}\n") { FontSize = 10 });
                    }
                    sumRow.Cells.Add(new TableCell(totalsPar));

                    sumRowGroup.Rows.Add(sumRow);
                    summaryTable.RowGroups.Add(sumRowGroup);
                    doc.Blocks.Add(summaryTable);

                    // 5. SIGNATURES ROW
                    var sigTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 20, 0, 10) };
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    var sigRowGroup = new TableRowGroup();
                    var sigRow = new TableRow();
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("-------------------------------------------\nCUSTOMER SIGNATURE")) { FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("-------------------------------------------\nAUTHORIZED SIGNATURE")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center }));
                    sigRowGroup.Rows.Add(sigRow);
                    sigTable.RowGroups.Add(sigRowGroup);
                    doc.Blocks.Add(sigTable);

                    // 6. FOOTER BAR
                    var footerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 0) };
                    footerTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    footerTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    var footRowGroup = new TableRowGroup();
                    var footRow = new TableRow();
                    footRow.Cells.Add(new TableCell(new Paragraph(new Run("* Thanks For Your Visit *")) { FontSize = 10, FontWeight = FontWeights.Bold }));
                    footRow.Cells.Add(new TableCell(new Paragraph(new Run("Software By: Roonjha Developers - 03152914836")) { FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    footRowGroup.Rows.Add(footRow);
                    footerTable.RowGroups.Add(footRowGroup);
                    doc.Blocks.Add(footerTable);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"A4 Invoice {invoice.InvoiceNumber ?? ""}");
                }
            });
        }

        public void PrintA4PurchaseInvoice(PurchaseInvoice invoice, CompanySetting company)
        {
            if (invoice == null) return;
            company ??= new CompanySetting();
            invoice.Items ??= new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    bool isReturn = invoice.Type == Core.Enums.PurchaseType.PurchaseReturn || (invoice.PurchaseNumber?.StartsWith("PUR-RET") == true);

                    var doc = new FlowDocument
                    {
                        PageWidth = 793,
                        PageHeight = 1122,
                        PagePadding = new Thickness(35),
                        ColumnWidth = 723,
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11
                    };

                    // =========================================================================
                    // 1. TOP HEADER BANNER (DARK BLUE #0B2A5A) MATCHING USER BANNER DESIGN
                    // =========================================================================
                    var headerBanner = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 14, 16, 14),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    var mainGrid = new Grid();
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Section (Logo + Big Calligraphy Header)
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // Spacer
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Section (Left Contacts | Middle Urdu Box | Right Contacts)

                    // TOP SECTION: LOGO (LEFT) & MAIN CALLIGRAPHY HEADING (RIGHT)
                    var topGrid = new Grid();
                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Left Logo Box
                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right Calligraphy Heading

                    // Left White Logo Box
                    var logoBox = new Border
                    {
                        Background = System.Windows.Media.Brushes.White,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(6),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 150
                    };
                    var logoStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    var logoImg = TryGetLogoImage(40);
                    if (logoImg != null)
                    {
                        logoImg.HorizontalAlignment = HorizontalAlignment.Center;
                        logoStack.Children.Add(logoImg);
                    }
                    else
                    {
                        logoStack.Children.Add(new TextBlock
                        {
                            Text = "🏛️ المدینہ",
                            FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                            TextAlignment = TextAlignment.Center
                        });
                    }
                    var logoBar = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 2, 4, 2),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    logoBar.Child = new TextBlock
                    {
                        Text = "بلڈنگ میٹریل اتھل",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center
                    };
                    logoStack.Children.Add(logoBar);
                    logoBox.Child = logoStack;
                    Grid.SetColumn(logoBox, 0);
                    topGrid.Children.Add(logoBox);

                    // Right Big Calligraphy Heading in White
                    var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                    titleStack.Children.Add(new TextBlock
                    {
                        Text = "المدینہ کنکریٹ بلاک ورکس اینڈ بلڈنگ میٹریل",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 23,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right
                    });
                    Grid.SetColumn(titleStack, 1);
                    topGrid.Children.Add(titleStack);

                    Grid.SetRow(topGrid, 0);
                    mainGrid.Children.Add(topGrid);

                    // BOTTOM SECTION: LEFT CONTACTS | MIDDLE URDU BOX | RIGHT CONTACTS
                    var bottomGrid = new Grid();
                    bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) }); // Left Contacts
                    bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Middle Urdu Box
                    bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); // Right Contacts

                    // Left Side Contacts
                    var leftContactStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };

                    var contactLine1 = new TextBlock { Margin = new Thickness(0, 0, 0, 2) };
                    contactLine1.Inlines.Add(new Run("0333-7970848  ") { FontFamily = new FontFamily("Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    contactLine1.Inlines.Add(new Run("ایم اقبال") { FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    leftContactStack.Children.Add(contactLine1);

                    var contactLine2 = new TextBlock();
                    contactLine2.Inlines.Add(new Run("0335-1279963  ") { FontFamily = new FontFamily("Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    contactLine2.Inlines.Add(new Run("ایم اکرم") { FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                    leftContactStack.Children.Add(contactLine2);

                    Grid.SetColumn(leftContactStack, 0);
                    bottomGrid.Children.Add(leftContactStack);

                    // Middle Urdu Description Box with White Border
                    var middleBox = new Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.White,
                        BorderThickness = new Thickness(1.2),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(8, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    var middleStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    middleStack.Children.Add(new TextBlock
                    {
                        Text = "ہمارے ہاں ہر سائز کا بلاک، سیمنٹ، بجری",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center
                    });
                    middleStack.Children.Add(new TextBlock
                    {
                        Text = "ریتی، روڑی، کرش اور سریا دستیاب ہے",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                    middleBox.Child = middleStack;
                    Grid.SetColumn(middleBox, 1);
                    bottomGrid.Children.Add(middleBox);

                    // Right Side Contacts
                    var rightContactStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                    rightContactStack.Children.Add(new TextBlock
                    {
                        Text = "ایم حسین",
                        FontFamily = new FontFamily("Jameel Noori Nastaleeq, Nafees Nastaleeq, Urdu Typesetting, Arial"),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right
                    });
                    rightContactStack.Children.Add(new TextBlock
                    {
                        Text = "0333-7980848",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 1, 0, 0)
                    });
                    rightContactStack.Children.Add(new TextBlock
                    {
                        Text = "0345-3799500",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 1, 0, 0)
                    });
                    Grid.SetColumn(rightContactStack, 2);
                    bottomGrid.Children.Add(rightContactStack);

                    Grid.SetRow(bottomGrid, 2);
                    mainGrid.Children.Add(bottomGrid);

                    headerBanner.Child = mainGrid;
                    doc.Blocks.Add(new BlockUIContainer(headerBanner));

                    // INVOICE TITLE BANNER: "PURCHASE INVOICE" / "PURCHASE RETURN"
                    var titleBanner = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B2A5A")),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(0, 4, 0, 4),
                        Margin = new Thickness(0, 2, 0, 12),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var titleBannerText = new TextBlock
                    {
                        Text = isReturn ? "PURCHASE RETURN" : "PURCHASE INVOICE",
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    };
                    titleBanner.Child = titleBannerText;
                    doc.Blocks.Add(new BlockUIContainer(titleBanner));

                    // 2. METADATA ROW (4 COLUMNS FULL WIDTH)
                    var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Vendor Name
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Purchase No
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Date
                    metaTable.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // Payment Terms / Mode

                    var metaGroup = new TableRowGroup();
                    var metaRow = new TableRow();

                    metaRow.Cells.Add(new TableCell(new Paragraph()
                    {
                        Inlines =
                        {
                            new Run("VENDOR NAME\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray },
                            new Run(string.IsNullOrWhiteSpace(invoice.DisplayVendorName) ? "Direct / Walk-in Vendor" : invoice.DisplayVendorName) { FontSize = 12, FontWeight = FontWeights.Bold }
                        }
                    }));

                    metaRow.Cells.Add(new TableCell(new Paragraph()
                    {
                        Inlines =
                        {
                            new Run(isReturn ? "RETURN NO\n" : "PURCHASE NO\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray },
                            new Run(invoice.PurchaseNumber ?? "") { FontSize = 12, FontWeight = FontWeights.Bold }
                        }
                    }));

                    metaRow.Cells.Add(new TableCell(new Paragraph()
                    {
                        Inlines =
                        {
                            new Run("DATE\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray },
                            new Run($"{invoice.Date:dd/MM/yyyy}") { FontSize = 12, FontWeight = FontWeights.Bold }
                        }
                    }));

                    var methodPill = new Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.RoyalBlue,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 3, 10, 3),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = System.Windows.Media.Brushes.LightSkyBlue
                    };
                    methodPill.Child = new TextBlock
                    {
                        Text = invoice.IsCashPurchase ? "CASH PURCHASE" : "CREDIT PURCHASE",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.MidnightBlue
                    };

                    var methodCell = new TableCell();
                    var methodPar = new Paragraph(new Run("PAYMENT TERMS\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Gray });
                    methodCell.Blocks.Add(methodPar);
                    methodCell.Blocks.Add(new BlockUIContainer(methodPill));
                    metaRow.Cells.Add(methodCell);

                    metaGroup.Rows.Add(metaRow);
                    metaTable.RowGroups.Add(metaGroup);
                    doc.Blocks.Add(metaTable);

                    // 3. PRODUCTS TABLE WITH FULL STAR WIDTHS
                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = System.Windows.Media.Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(0.4, GridUnitType.Star) }); // #
                    table.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) }); // ITEM DESCRIPTION
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // UNIT
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // QUANTITY
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) }); // RATE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // TOTAL

                    var rowGroup = new TableRowGroup();
                    var headerRow = new TableRow { Background = System.Windows.Media.Brushes.WhiteSmoke };

                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("#")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6) }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("ITEM DESCRIPTION")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6) }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("UNIT")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Center }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("QUANTITY")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Center }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("RATE")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Right }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, FontSize = 9.5, Margin = new Thickness(4, 6, 4, 6), TextAlignment = TextAlignment.Right }));
                    rowGroup.Rows.Add(headerRow);

                    int idx = 1;
                    foreach (var item in invoice.Items)
                    {
                        if (item == null) continue;
                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run(idx.ToString())) { FontSize = 10, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(item.ItemName ?? "")) { FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(string.IsNullOrWhiteSpace(item.UnitName) ? "Pcs" : item.UnitName)) { FontSize = 10, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.Quantity:0.##}")) { FontSize = 10, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"PKR {item.Rate:N0}")) { FontSize = 10, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"PKR {item.TotalPrice:N0}")) { FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                        rowGroup.Rows.Add(row);
                        idx++;
                    }
                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    // 4. LOWER SUMMARY: TERMS (LEFT) & TOTALS (RIGHT)
                    var summaryTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 14) };
                    summaryTable.Columns.Add(new TableColumn { Width = new GridLength(4.5, GridUnitType.Star) }); // Notes/Terms
                    summaryTable.Columns.Add(new TableColumn { Width = new GridLength(2.7, GridUnitType.Star) }); // Totals

                    var sumRowGroup = new TableRowGroup();
                    var sumRow = new TableRow();

                    // LEFT CELL: TERMS
                    var termsPar = new Paragraph();
                    termsPar.Inlines.Add(new Run("Notes / Remarks:\n") { FontWeight = FontWeights.Bold, FontSize = 10 });
                    termsPar.Inlines.Add(new Run(string.IsNullOrWhiteSpace(invoice.Remarks) ? "Purchase recorded in Al Madina ERP." : invoice.Remarks) { FontSize = 9.5 });
                    sumRow.Cells.Add(new TableCell(termsPar));

                    // RIGHT CELL: TOTALS SUMMARY
                    var totalsPar = new Paragraph();
                    totalsPar.TextAlignment = TextAlignment.Right;
                    totalsPar.Inlines.Add(new Run($"Subtotal:   PKR {invoice.Subtotal:N2}\n") { FontSize = 10 });
                    if (invoice.VehicleCharges > 0)
                        totalsPar.Inlines.Add(new Run($"Vehicle Charges:   PKR {invoice.VehicleCharges:N2}\n") { FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.DarkSlateBlue });
                    if (invoice.ExtraExpenses > 0)
                        totalsPar.Inlines.Add(new Run($"Extra Expenses:   PKR {invoice.ExtraExpenses:N2}\n") { FontSize = 10 });
                    if (invoice.DiscountAmount > 0)
                        totalsPar.Inlines.Add(new Run($"Discount (-):   PKR {invoice.DiscountAmount:N2}\n") { FontSize = 10 });
                    totalsPar.Inlines.Add(new Run($"INVOICE TOTAL:   PKR {invoice.TotalAmount:N2}\n") { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.MidnightBlue });
                    
                    decimal a4RemainingBal = Math.Max(0m, invoice.TotalAmount - invoice.AmountPaid);
                    totalsPar.Inlines.Add(new Run($"Amount Paid:   PKR {invoice.AmountPaid:N2}\n") { FontSize = 10 });

                    if (a4RemainingBal > 0)
                    {
                        totalsPar.Inlines.Add(new Run($"Balance Due:   PKR {a4RemainingBal:N2}\n") { FontSize = 11, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkRed });
                    }
                    else
                    {
                        totalsPar.Inlines.Add(new Run($"Status:   PAID IN FULL\n") { FontSize = 10, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkGreen });
                    }
                    sumRow.Cells.Add(new TableCell(totalsPar));

                    sumRowGroup.Rows.Add(sumRow);
                    summaryTable.RowGroups.Add(sumRowGroup);
                    doc.Blocks.Add(summaryTable);

                    // 5. SIGNATURES ROW
                    var sigTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 20, 0, 10) };
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    var sigRowGroup = new TableRowGroup();
                    var sigRow = new TableRow();
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("-------------------------------------------\nVENDOR SIGNATURE")) { FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("-------------------------------------------\nAUTHORIZED RECEIVER SIGNATURE")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center }));
                    sigRowGroup.Rows.Add(sigRow);
                    sigTable.RowGroups.Add(sigRowGroup);
                    doc.Blocks.Add(sigTable);

                    // 6. FOOTER BAR
                    var footerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 0) };
                    footerTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    footerTable.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    var footRowGroup = new TableRowGroup();
                    var footRow = new TableRow();
                    footRow.Cells.Add(new TableCell(new Paragraph(new Run("* Al Madina Building Material *")) { FontSize = 10, FontWeight = FontWeights.Bold }));
                    footRow.Cells.Add(new TableCell(new Paragraph(new Run("Software By: Roonjha Developers - 03152914836")) { FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    footRowGroup.Rows.Add(footRow);
                    footerTable.RowGroups.Add(footRowGroup);
                    doc.Blocks.Add(footerTable);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Purchase Invoice {invoice.PurchaseNumber ?? ""}");
                }
            });
        }

        public void PrintReceiptVoucher(Receipt receipt, CompanySetting company)
        {
            if (receipt == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth,
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Arial, Times New Roman, sans-serif"),
                        FontSize = 10
                    };

                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName.ToUpper();
                    var vTypeTitle = receipt.ReceiptType == ReceiptType.BankReceipt ? "BANK RECEIPT VOUCHER" : "CASH RECEIPT VOUCHER";

                    // 1. Executive Header
                    var logo = TryGetLogoImage(100);
                    if (logo != null) doc.Blocks.Add(new BlockUIContainer(logo));

                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(122, 12, 12)) });
                    headerPara.Inlines.Add(new Run($"{vTypeTitle}\n") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)) });
                    headerPara.Inlines.Add(new Run($"Voucher #: {receipt.ReceiptNumber}   |   Date: {receipt.Date:dd/MM/yyyy}\n") { FontSize = 10, FontWeight = FontWeights.Bold });
                    headerPara.Inlines.Add(new Run($"Phone: {company.Phone ?? "03351279963"}   |   Address: {company.Address ?? "Main Bazaar, Uthal"}\n") { FontSize = 9, Foreground = System.Windows.Media.Brushes.DimGray });
                    doc.Blocks.Add(headerPara);

                    // 2. Voucher Details Table
                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(1), BorderBrush = System.Windows.Media.Brushes.SlateGray, Margin = new Thickness(0, 4, 0, 16) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(4.2, GridUnitType.Star) });

                    var rowGroup = new TableRowGroup();
                    void AddDetailRow(string label, string value, bool isBold = false, System.Windows.Media.Brush? textBrush = null)
                    {
                        var r = new TableRow();
                        var lblCell = new TableCell(new Paragraph(new Run(label)) { FontWeight = FontWeights.Bold, FontSize = 10, Margin = new Thickness(8, 6, 8, 6) })
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                            BorderThickness = new Thickness(0, 0, 1, 1),
                            BorderBrush = System.Windows.Media.Brushes.LightGray
                        };
                        var valCell = new TableCell(new Paragraph(new Run(value ?? "-")) { FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal, FontSize = isBold ? 11 : 10, Foreground = textBrush ?? System.Windows.Media.Brushes.Black, Margin = new Thickness(8, 6, 8, 6) })
                        {
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            BorderBrush = System.Windows.Media.Brushes.LightGray
                        };
                        r.Cells.Add(lblCell);
                        r.Cells.Add(valCell);
                        rowGroup.Rows.Add(r);
                    }

                    AddDetailRow("Voucher Number", receipt.ReceiptNumber, true);
                    AddDetailRow("Date & Time", receipt.Date.ToString("dd-MMM-yyyy  hh:mm tt"));
                    AddDetailRow("Received From (Customer)", string.IsNullOrWhiteSpace(receipt.CustomerName) ? "WALK-IN CUSTOMER" : receipt.CustomerName.ToUpper(), true);
                    AddDetailRow("Deposit Mode / Method", receipt.ReceivedBy ?? receipt.PaymentMethod.ToString());
                    if (!string.IsNullOrWhiteSpace(receipt.BankName)) AddDetailRow("Bank Account", receipt.BankName);
                    if (!string.IsNullOrWhiteSpace(receipt.ChequeNo)) AddDetailRow("Cheque / Ref No", receipt.ChequeNo);
                    if (!string.IsNullOrWhiteSpace(receipt.ReferenceNumber)) AddDetailRow("Reference Number", receipt.ReferenceNumber);
                    AddDetailRow("Remarks / Narration", receipt.Remarks);
                    AddDetailRow("Status", receipt.Status ?? "Posted", true, System.Windows.Media.Brushes.DarkGreen);
                    AddDetailRow("Total Amount Received", $"PKR {receipt.Amount:N2}", true, System.Windows.Media.Brushes.DarkGreen);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    // 3. Highlighted Net Amount Box
                    var amtBorder = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(16, 10, 16, 10),
                        Margin = new Thickness(0, 0, 0, 30)
                    };
                    var amtGrid = new Grid();
                    amtGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    amtGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var lblAmt = new TextBlock { Text = "NET AMOUNT RECEIVED (PKR)", Foreground = System.Windows.Media.Brushes.LightGray, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
                    var valAmt = new TextBlock { Text = $"PKR {receipt.Amount:N2}", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128)), FontSize = 20, FontWeight = FontWeights.Bold };
                    Grid.SetColumn(lblAmt, 0); Grid.SetColumn(valAmt, 1);
                    amtGrid.Children.Add(lblAmt); amtGrid.Children.Add(valAmt);
                    amtBorder.Child = amtGrid;
                    doc.Blocks.Add(new BlockUIContainer(amtBorder));

                    // 4. Signatures Row
                    var sigTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 30, 0, 10) };
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    var sigGroup = new TableRowGroup();
                    var sigRow = new TableRow();
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("_______________________\nPrepared By")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("_______________________\nCustomer Signature")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("_______________________\nAuthorized Signature")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigGroup.Rows.Add(sigRow);
                    sigTable.RowGroups.Add(sigGroup);
                    doc.Blocks.Add(sigTable);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Receipt {receipt.ReceiptNumber}");
                }
            });
        }

        public void PrintPaymentVoucher(Payment payment, CompanySetting company)
        {
            if (payment == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth,
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Arial, Times New Roman, sans-serif"),
                        FontSize = 10
                    };

                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName.ToUpper();
                    var vTypeTitle = payment.PaymentType == PaymentType.BankPayment ? "BANK PAYMENT VOUCHER" : "CASH PAYMENT VOUCHER";

                    // 1. Executive Header
                    var logo = TryGetLogoImage(100);
                    if (logo != null) doc.Blocks.Add(new BlockUIContainer(logo));

                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(122, 12, 12)) });
                    headerPara.Inlines.Add(new Run($"{vTypeTitle}\n") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)) });
                    headerPara.Inlines.Add(new Run($"Voucher #: {payment.PaymentNumber}   |   Date: {payment.Date:dd/MM/yyyy}\n") { FontSize = 10, FontWeight = FontWeights.Bold });
                    headerPara.Inlines.Add(new Run($"Phone: {company.Phone ?? "03351279963"}   |   Address: {company.Address ?? "Main Bazaar, Uthal"}\n") { FontSize = 9, Foreground = System.Windows.Media.Brushes.DimGray });
                    doc.Blocks.Add(headerPara);

                    // 2. Voucher Details Table
                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(1), BorderBrush = System.Windows.Media.Brushes.SlateGray, Margin = new Thickness(0, 4, 0, 16) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(4.2, GridUnitType.Star) });

                    var rowGroup = new TableRowGroup();
                    void AddDetailRow(string label, string value, bool isBold = false, System.Windows.Media.Brush? textBrush = null)
                    {
                        var r = new TableRow();
                        var lblCell = new TableCell(new Paragraph(new Run(label)) { FontWeight = FontWeights.Bold, FontSize = 10, Margin = new Thickness(8, 6, 8, 6) })
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                            BorderThickness = new Thickness(0, 0, 1, 1),
                            BorderBrush = System.Windows.Media.Brushes.LightGray
                        };
                        var valCell = new TableCell(new Paragraph(new Run(value ?? "-")) { FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal, FontSize = isBold ? 11 : 10, Foreground = textBrush ?? System.Windows.Media.Brushes.Black, Margin = new Thickness(8, 6, 8, 6) })
                        {
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            BorderBrush = System.Windows.Media.Brushes.LightGray
                        };
                        r.Cells.Add(lblCell);
                        r.Cells.Add(valCell);
                        rowGroup.Rows.Add(r);
                    }

                    AddDetailRow("Voucher Number", payment.PaymentNumber, true);
                    AddDetailRow("Date & Time", payment.Date.ToString("dd-MMM-yyyy  hh:mm tt"));
                    AddDetailRow("Paid To (Party / Payee)", string.IsNullOrWhiteSpace(payment.PartyName) ? "SUPPLIER / PARTY" : payment.PartyName.ToUpper(), true);
                    AddDetailRow("Paid From / Mode", payment.PaidFrom ?? payment.PaymentMethod.ToString());
                    if (!string.IsNullOrWhiteSpace(payment.BankName)) AddDetailRow("Bank Account", payment.BankName);
                    if (!string.IsNullOrWhiteSpace(payment.ChequeNo)) AddDetailRow("Cheque / Ref No", payment.ChequeNo);
                    if (payment.ChequeDate.HasValue) AddDetailRow("Cheque Date", payment.ChequeDate.Value.ToString("dd/MM/yyyy"));
                    if (!string.IsNullOrWhiteSpace(payment.Narration)) AddDetailRow("Narration", payment.Narration);
                    if (!string.IsNullOrWhiteSpace(payment.Remarks)) AddDetailRow("Remarks", payment.Remarks);
                    AddDetailRow("Status", payment.Status ?? "Posted", true, System.Windows.Media.Brushes.DarkRed);
                    AddDetailRow("Total Gross Amount", $"PKR {payment.Amount:N2}", true);
                    if (payment.WhtAmount > 0) AddDetailRow($"WHT Tax ({payment.WhtRatePercent:0.##}%)", $"PKR {payment.WhtAmount:N2}");
                    AddDetailRow("Net Amount Paid", $"PKR {payment.NetAmountToPay:N2}", true, System.Windows.Media.Brushes.DarkRed);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    // 3. Highlighted Net Amount Box
                    var amtBorder = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(16, 10, 16, 10),
                        Margin = new Thickness(0, 0, 0, 30)
                    };
                    var amtGrid = new Grid();
                    amtGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    amtGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var lblAmt = new TextBlock { Text = "NET AMOUNT PAID (PKR)", Foreground = System.Windows.Media.Brushes.LightGray, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
                    var valAmt = new TextBlock { Text = $"PKR {payment.NetAmountToPay:N2}", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 114, 182)), FontSize = 20, FontWeight = FontWeights.Bold };
                    Grid.SetColumn(lblAmt, 0); Grid.SetColumn(valAmt, 1);
                    amtGrid.Children.Add(lblAmt); amtGrid.Children.Add(valAmt);
                    amtBorder.Child = amtGrid;
                    doc.Blocks.Add(new BlockUIContainer(amtBorder));

                    // 4. Signatures Row
                    var sigTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 30, 0, 10) };
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    var sigGroup = new TableRowGroup();
                    var sigRow = new TableRow();
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("_______________________\nPrepared By")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("_______________________\nPayee Signature")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigRow.Cells.Add(new TableCell(new Paragraph(new Run("_______________________\nAuthorized Signature")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.Bold }));
                    sigGroup.Rows.Add(sigRow);
                    sigTable.RowGroups.Add(sigGroup);
                    doc.Blocks.Add(sigTable);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Payment {payment.PaymentNumber}");
                }
            });
        }

        public void PrintCustomerLedger(CustomerBalanceDto customer, System.Collections.Generic.IEnumerable<CustomerLedger> entries, CompanySetting company)
        {
            if (customer == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth, // CRITICAL FIX: Forces full A4 page width, prevents multi-column squeezing!
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Times New Roman, Arial, sans-serif"),
                        FontSize = 9.5
                    };

                    var entryList = (entries ?? System.Linq.Enumerable.Empty<CustomerLedger>()).ToList();
                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL ERP" : company.CompanyName.ToUpper();

                    // 1. CENTERED EXECUTIVE HEADER (EXACTLY MATCHING IMAGE 2)
                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"CUSTOMER LEDGER STATEMENT - {customer.Name.ToUpper()} ({customer.Code})\n") { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });

                    string netBalStr = customer.CustomerOwes > 0 ? $"PKR -{customer.CustomerOwes:N2}" : $"PKR {customer.AdvanceAvailable:N2}";
                    headerPara.Inlines.Add(new Run($"Phone: {customer.Phone ?? company.Phone ?? "N/A"}  |  Current Net Balance: {netBalStr}\n") { FontSize = 9.5, FontWeight = FontWeights.Bold });
                    headerPara.Inlines.Add(new Run($"Statement Date: {DateTime.Now:dd/MM/yyyy HH:mm}  |  Total Entries: {entryList.Count}") { FontSize = 9, Foreground = System.Windows.Media.Brushes.DimGray });

                    doc.Blocks.Add(headerPara);

                    // 2. LEDGER TABLE WITH 7 FULL-WIDTH STAR COLUMNS (EXACT MATCHING IMAGE 2)
                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)), Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // DATE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) }); // VOUCHER #
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // TYPE
                    table.Columns.Add(new TableColumn { Width = new GridLength(2.5, GridUnitType.Star) }); // DESCRIPTION
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // DEBIT (PKR)
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // CREDIT (PKR)
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // BALANCE (PKR)

                    var rowGroup = new TableRowGroup();
                    var headerRowTable = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) };

                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DATE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("VOUCHER #")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("TYPE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DESCRIPTION")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DEBIT (PKR)")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("CREDIT (PKR)")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("BALANCE (PKR)")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    rowGroup.Rows.Add(headerRowTable);

                    decimal totDebit = 0m, totCredit = 0m;
                    int rIdx = 0;
                    foreach (var e in entryList)
                    {
                        totDebit += e.Debit;
                        totCredit += e.Credit;

                        var bgBrush = rIdx % 2 == 0 ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251));
                        var row = new TableRow { Background = bgBrush };

                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Date.ToString("dd/MM/yyyy"))) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.VoucherNumber ?? "")) { FontSize = 8.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.TransactionType ?? "")) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));

                        string desc = !string.IsNullOrWhiteSpace(e.Remarks) ? e.Remarks : (!string.IsNullOrWhiteSpace(e.ItemDetailsSummary) ? e.ItemDetailsSummary : "");
                        row.Cells.Add(new TableCell(new Paragraph(new Run(desc)) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));

                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Debit > 0 ? e.Debit.ToString("N2") : "-")) { FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Credit > 0 ? e.Credit.ToString("N2") : "-")) { FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));

                        string balDisplay = e.RunningBalance < 0 ? $"-{Math.Abs(e.RunningBalance):N2}" : e.RunningBalance.ToString("N2");
                        row.Cells.Add(new TableCell(new Paragraph(new Run(balDisplay)) { FontSize = 8.5, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));
                        rowGroup.Rows.Add(row);

                        if (e.SaleInvoice?.Items != null && e.SaleInvoice.Items.Count > 0)
                        {
                            foreach (var item in e.SaleInvoice.Items)
                            {
                                var itemRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 247, 255)) };
                                string itemDesc = $"└─ Item: {item.ItemName} | Qty: {item.Quantity:0.#} {item.UnitName} | Price/Unit: Rs.{item.Rate:N2} | Amount: Rs.{item.TotalPrice:N2}";
                                if (item.IsSpecialLengthItem && item.LengthFeet > 0)
                                {
                                    itemDesc = $"└─ Item: {item.ItemName} | Qty: {item.Quantity:0.#} pcs ({item.LengthFeet:0.##} ft) | Price/Unit: Rs.{item.RatePerFoot:N2}/ft | Amount: Rs.{item.TotalPrice:N2}";
                                }
                                var itemCell = new TableCell(new Paragraph(new Run(itemDesc))
                                {
                                    FontSize = 8,
                                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
                                    FontStyle = FontStyles.Italic,
                                    Margin = new Thickness(15, 2, 4, 2)
                                })
                                {
                                    ColumnSpan = 7
                                };
                                itemRow.Cells.Add(itemCell);
                                rowGroup.Rows.Add(itemRow);
                            }
                        }
                        rIdx++;
                    }

                    // TOTALS ROW (EXACT MATCHING IMAGE 2)
                    var totalRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)) };
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, FontSize = 9, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(totDebit.ToString("N2"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(totCredit.ToString("N2"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(netBalStr)) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Customer Ledger {customer.Name}");
                }
            });
        }

        public void PrintVendorLedger(VendorBalanceDto vendor, System.Collections.Generic.IEnumerable<VendorLedger> entries, CompanySetting company)
        {
            if (vendor == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth, // CRITICAL FIX: Forces full A4 page width, prevents multi-column squeezing!
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Times New Roman, Arial, sans-serif"),
                        FontSize = 9.5
                    };

                    var entryList = (entries ?? System.Linq.Enumerable.Empty<VendorLedger>()).ToList();
                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL ERP" : company.CompanyName.ToUpper();

                    // 1. CENTERED EXECUTIVE HEADER (EXACTLY MATCHING IMAGE 2)
                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"VENDOR LEDGER STATEMENT - {vendor.Name.ToUpper()} ({vendor.Code})\n") { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });

                    string netBalStr = vendor.VendorOwes > 0 ? $"PKR {vendor.VendorOwes:N2}" : $"PKR -{vendor.AdvanceAvailable:N2}";
                    headerPara.Inlines.Add(new Run($"Phone: {vendor.Phone ?? company.Phone ?? "N/A"}  |  Current Net Balance: {netBalStr}\n") { FontSize = 9.5, FontWeight = FontWeights.Bold });
                    headerPara.Inlines.Add(new Run($"Statement Date: {DateTime.Now:dd/MM/yyyy HH:mm}  |  Total Entries: {entryList.Count}") { FontSize = 9, Foreground = System.Windows.Media.Brushes.DimGray });

                    doc.Blocks.Add(headerPara);

                    // 2. LEDGER TABLE WITH 7 FULL-WIDTH STAR COLUMNS (EXACT MATCHING IMAGE 2)
                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)), Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // DATE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) }); // VOUCHER #
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // TYPE
                    table.Columns.Add(new TableColumn { Width = new GridLength(2.5, GridUnitType.Star) }); // DESCRIPTION
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // DEBIT (PKR)
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // CREDIT (PKR)
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // BALANCE (PKR)

                    var rowGroup = new TableRowGroup();
                    var headerRowTable = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) };

                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DATE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("VOUCHER #")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("TYPE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DESCRIPTION")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DEBIT (PKR)")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("CREDIT (PKR)")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("BALANCE (PKR)")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    rowGroup.Rows.Add(headerRowTable);

                    decimal totDebit = 0m, totCredit = 0m;
                    int rIdx = 0;
                    foreach (var e in entryList)
                    {
                        totDebit += e.Debit;
                        totCredit += e.Credit;

                        var bgBrush = rIdx % 2 == 0 ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251));
                        var row = new TableRow { Background = bgBrush };

                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Date.ToString("dd/MM/yyyy"))) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.VoucherNumber ?? "")) { FontSize = 8.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.TransactionType ?? "")) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));

                        string desc = !string.IsNullOrWhiteSpace(e.Remarks) ? e.Remarks : (!string.IsNullOrWhiteSpace(e.ItemDetailsSummary) ? e.ItemDetailsSummary : "");
                        row.Cells.Add(new TableCell(new Paragraph(new Run(desc)) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));

                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Debit > 0 ? e.Debit.ToString("N2") : "-")) { FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Credit > 0 ? e.Credit.ToString("N2") : "-")) { FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));

                        string balDisplay = e.RunningBalance < 0 ? $"-{Math.Abs(e.RunningBalance):N2}" : e.RunningBalance.ToString("N2");
                        row.Cells.Add(new TableCell(new Paragraph(new Run(balDisplay)) { FontSize = 8.5, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));
                        rowGroup.Rows.Add(row);

                        if (e.PurchaseInvoice?.Items != null && e.PurchaseInvoice.Items.Count > 0)
                        {
                            foreach (var item in e.PurchaseInvoice.Items)
                            {
                                var itemRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 247, 255)) };
                                string itemDesc = $"└─ Item: {item.ItemName} | Qty: {item.Quantity:0.#} {item.UnitName} | Price/Unit: Rs.{item.Rate:N2} | Amount: Rs.{item.TotalPrice:N2}";
                                if (item.IsSpecialLengthItem && item.LengthFeet > 0)
                                {
                                    itemDesc = $"└─ Item: {item.ItemName} | Qty: {item.Quantity:0.#} pcs ({item.LengthFeet:0.##} ft) | Price/Unit: Rs.{item.RatePerFoot:N2}/ft | Amount: Rs.{item.TotalPrice:N2}";
                                }
                                var itemCell = new TableCell(new Paragraph(new Run(itemDesc))
                                {
                                    FontSize = 8,
                                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
                                    FontStyle = FontStyles.Italic,
                                    Margin = new Thickness(15, 2, 4, 2)
                                })
                                {
                                    ColumnSpan = 7
                                };
                                itemRow.Cells.Add(itemCell);
                                rowGroup.Rows.Add(itemRow);
                            }
                        }
                        rIdx++;
                    }

                    // TOTALS ROW (EXACT MATCHING IMAGE 2)
                    var totalRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)) };
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, FontSize = 9, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(totDebit.ToString("N2"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(totCredit.ToString("N2"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(netBalStr)) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Vendor Ledger {vendor.Name}");
                }
            });
        }

        public void PrintInventoryLedger(Item item, System.Collections.Generic.IEnumerable<InventoryLedger> entries, CompanySetting company)
        {
            if (item == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth,
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Times New Roman, Arial, sans-serif"),
                        FontSize = 9.5
                    };

                    var logo = TryGetLogoImage(100);
                    if (logo != null) doc.Blocks.Add(new BlockUIContainer(logo));

                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL ERP" : company.CompanyName.ToUpper();
                    var unitStr = item.SaleUnit?.ShortCode ?? "Pcs";
                    var entryList = (entries ?? System.Linq.Enumerable.Empty<InventoryLedger>()).ToList();

                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"INVENTORY LEDGER REPORT — {item.Name.ToUpper()} ({item.Code})\n") { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"Unit: {unitStr}  |  Current Stock: {item.CurrentStock:N0} {unitStr}  |  Printed: {DateTime.Now:dd/MM/yyyy HH:mm}\n") { FontSize = 9.5, FontWeight = FontWeights.Bold });
                    doc.Blocks.Add(headerPara);

                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)), Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) }); // DATE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // TRAN #
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // TYPE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // QTY IN
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // QTY OUT
                    table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) }); // UNIT
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // BALANCE
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // REMARKS

                    var rowGroup = new TableRowGroup();
                    var headerRowTable = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) };
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("DATE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("TRAN #")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("TYPE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("QTY IN")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("QTY OUT")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("UNIT")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("BALANCE")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 5, 4, 5) }));
                    headerRowTable.Cells.Add(new TableCell(new Paragraph(new Run("REMARKS")) { FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 8.5, Margin = new Thickness(4, 5, 4, 5) }));
                    rowGroup.Rows.Add(headerRowTable);

                    decimal totIn = 0m, totOut = 0m;
                    int rIdx = 0;
                    foreach (var e in entryList)
                    {
                        totIn += e.QuantityIn;
                        totOut += e.QuantityOut;

                        var bgBrush = rIdx % 2 == 0 ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251));
                        var row = new TableRow { Background = bgBrush };

                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Date.ToString("dd/MM/yyyy HH:mm"))) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.VoucherNumber ?? "")) { FontSize = 8.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.TransactionType ?? "")) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.QuantityIn > 0 ? e.QuantityIn.ToString("N0") : "-")) { FontSize = 8.5, TextAlignment = TextAlignment.Right, Foreground = System.Windows.Media.Brushes.DarkGreen, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.QuantityOut > 0 ? e.QuantityOut.ToString("N0") : "-")) { FontSize = 8.5, TextAlignment = TextAlignment.Right, Foreground = System.Windows.Media.Brushes.DarkRed, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Unit ?? unitStr)) { FontSize = 8.5, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.RunningBalance.ToString("N0"))) { FontSize = 8.5, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 4, 4, 4) }));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(e.Remarks ?? "")) { FontSize = 8.5, Margin = new Thickness(4, 4, 4, 4) }));
                        rowGroup.Rows.Add(row);
                        rIdx++;
                    }

                    var totalRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)) };
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, FontSize = 9, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(totIn.ToString("N0"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Foreground = System.Windows.Media.Brushes.DarkGreen, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(totOut.ToString("N0"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Foreground = System.Windows.Media.Brushes.DarkRed, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(unitStr)) { FontSize = 9, TextAlignment = TextAlignment.Center, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(item.CurrentStock.ToString("N0"))) { FontWeight = FontWeights.Bold, FontSize = 9, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 6, 4, 6) }));
                    totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Inventory Ledger {item.Name}");
                }
            });
        }

        public void PrintCustomerOrder(CustomerOrder order, CompanySetting company)
        {
            if (order == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth,
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Times New Roman, Arial, sans-serif"),
                        FontSize = 10
                    };

                    var logo = TryGetLogoImage(100);
                    if (logo != null) doc.Blocks.Add(new BlockUIContainer(logo));

                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName.ToUpper();
                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run("CUSTOMER ORDER VOUCHER\n") { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"Order #: {order.OrderNumber}  |  Date: {order.OrderDate:dd/MM/yyyy}  |  Status: {order.Status}\n") { FontSize = 10, FontWeight = FontWeights.Bold });
                    doc.Blocks.Add(headerPara);

                    var infoPara = new Paragraph { Margin = new Thickness(0, 0, 0, 14), FontSize = 10 };
                    infoPara.Inlines.Add(new Run("Customer Name: ") { FontWeight = FontWeights.Bold });
                    infoPara.Inlines.Add(new Run($"{order.CustomerName}\n"));
                    infoPara.Inlines.Add(new Run("Contact Number: ") { FontWeight = FontWeights.Bold });
                    infoPara.Inlines.Add(new Run($"{order.ContactNumber}\n"));
                    infoPara.Inlines.Add(new Run("Delivery Address: ") { FontWeight = FontWeights.Bold });
                    infoPara.Inlines.Add(new Run($"{order.Address}\n"));
                    if (order.ReceivingDate.HasValue)
                    {
                        infoPara.Inlines.Add(new Run("Receiving Date: ") { FontWeight = FontWeights.Bold });
                        infoPara.Inlines.Add(new Run($"{order.ReceivingDate.Value:dd/MM/yyyy}\n"));
                    }
                    doc.Blocks.Add(infoPara);

                    var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = System.Windows.Media.Brushes.Black, Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(0.6, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(2.5, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) });

                    var headerGroup = new TableRowGroup();
                    var hRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)) };
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Sr #")) { FontWeight = FontWeights.Bold }));
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Item Description")) { FontWeight = FontWeights.Bold }));
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Qty")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Length")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Unit")) { FontWeight = FontWeights.Bold }));
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Rate (PKR)")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    hRow.Cells.Add(new TableCell(new Paragraph(new Run("Total (PKR)")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    headerGroup.Rows.Add(hRow);
                    table.RowGroups.Add(headerGroup);

                    var bodyGroup = new TableRowGroup();
                    int sr = 1;
                    foreach (var item in order.Items)
                    {
                        var bRow = new TableRow();
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run(sr++.ToString()))));
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run(item.ItemNameSnapshot))));
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run($"{item.Quantity:N0}")) { TextAlignment = TextAlignment.Right }));
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run(item.IsLengthBased && item.LengthFeet > 0 ? $"{item.LengthFeet:N1} ft" : "-")) { TextAlignment = TextAlignment.Right }));
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Unit))));
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run($"{item.Rate:N2}")) { TextAlignment = TextAlignment.Right }));
                        bRow.Cells.Add(new TableCell(new Paragraph(new Run($"{item.LineTotal:N2}")) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold }));
                        bodyGroup.Rows.Add(bRow);
                    }
                    table.RowGroups.Add(bodyGroup);
                    doc.Blocks.Add(table);

                    var summaryPara = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 10, 0, 0), FontSize = 11 };
                    summaryPara.Inlines.Add(new Run("Grand Total: ") { FontWeight = FontWeights.Bold });
                    summaryPara.Inlines.Add(new Run($"Rs. {order.TotalAmount:N2}\n") { FontWeight = FontWeights.Bold, FontSize = 14, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    summaryPara.Inlines.Add(new Run($"Amount Paid: Rs. {order.PaidAmount:N2}   |   Remaining Balance: Rs. {order.RemainingAmount:N2}\n") { FontWeight = FontWeights.Bold });
                    doc.Blocks.Add(summaryPara);

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Customer Order {order.OrderNumber}");
                }
            });
        }

        public void PrintReportTable(string title, System.Collections.Generic.IEnumerable<string> headers, System.Collections.Generic.IEnumerable<System.Collections.Generic.IEnumerable<string>> rows, System.Collections.Generic.IEnumerable<string>? totalsRow, CompanySetting company)
        {
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    double printableWidth = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : 793;
                    double printableHeight = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : 1122;

                    var doc = new FlowDocument
                    {
                        PageWidth = printableWidth,
                        PageHeight = printableHeight,
                        ColumnWidth = printableWidth,
                        PagePadding = new Thickness(40, 30, 40, 30),
                        FontFamily = new FontFamily("Times New Roman, Arial, sans-serif"),
                        FontSize = 9.5
                    };

                    var logo = TryGetLogoImage(100);
                    if (logo != null) doc.Blocks.Add(new BlockUIContainer(logo));

                    var compName = string.IsNullOrWhiteSpace(company.CompanyName) ? "AL MADINA BUILDING MATERIAL ERP" : company.CompanyName.ToUpper();
                    var headerList = System.Linq.Enumerable.ToList(headers ?? System.Linq.Enumerable.Empty<string>());
                    var rowsList = System.Linq.Enumerable.ToList(rows ?? System.Linq.Enumerable.Empty<System.Collections.Generic.IEnumerable<string>>());

                    var headerPara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    headerPara.Inlines.Add(new Run(compName + "\n") { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"{title.ToUpper()}\n") { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) });
                    headerPara.Inlines.Add(new Run($"Printed Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Records: {rowsList.Count}\n") { FontSize = 9, Foreground = System.Windows.Media.Brushes.DimGray });
                    doc.Blocks.Add(headerPara);

                    if (headerList.Count > 0)
                    {
                        var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)), Margin = new Thickness(0, 0, 0, 14) };

                        // Proportional star columns tailored to header count
                        for (int i = 0; i < headerList.Count; i++)
                        {
                            var hText = headerList[i].ToLower();
                            double weight = 1.0;
                            if (hText.Contains("name") || hText.Contains("description") || hText.Contains("item") || hText.Contains("party")) weight = 2.4;
                            else if (hText.Contains("code") || hText.Contains("voucher") || hText.Contains("ref")) weight = 1.2;
                            else if (hText.Contains("phone") || hText.Contains("date")) weight = 1.1;
                            else if (hText.Contains("unit") || hText.Contains("qty") || hText.Contains("status")) weight = 0.9;
                            else weight = 1.3;

                            table.Columns.Add(new TableColumn { Width = new GridLength(weight, GridUnitType.Star) });
                        }

                        var rowGroup = new TableRowGroup();
                        var headerRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 0, 0)) };
                        foreach (var h in headerList)
                        {
                            bool isRightAlign = h.ToLower().Contains("amount") || h.ToLower().Contains("price") || h.ToLower().Contains("rate") || h.ToLower().Contains("val") || h.ToLower().Contains("profit") || h.ToLower().Contains("debit") || h.ToLower().Contains("credit") || h.ToLower().Contains("owed") || h.ToLower().Contains("payable") || h.ToLower().Contains("receivable") || h.ToLower().Contains("advance");
                            var cell = new TableCell(new Paragraph(new Run(h.ToUpper()))
                            {
                                FontWeight = FontWeights.Bold,
                                Foreground = System.Windows.Media.Brushes.White,
                                FontSize = 8.5,
                                TextAlignment = isRightAlign ? TextAlignment.Right : TextAlignment.Left,
                                Margin = new Thickness(4, 5, 4, 5)
                            });
                            headerRow.Cells.Add(cell);
                        }
                        rowGroup.Rows.Add(headerRow);

                        int rowIdx = 0;
                        foreach (var r in rowsList)
                        {
                            var bg = (rowIdx % 2 == 1) ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)) : System.Windows.Media.Brushes.White;
                            var row = new TableRow { Background = bg };
                            int colIdx = 0;
                            foreach (var cellVal in r)
                            {
                                var h = colIdx < headerList.Count ? headerList[colIdx].ToLower() : "";
                                bool isRightAlign = h.Contains("amount") || h.Contains("price") || h.Contains("rate") || h.Contains("val") || h.Contains("profit") || h.Contains("debit") || h.Contains("credit") || h.Contains("owed") || h.Contains("payable") || h.Contains("receivable") || h.Contains("advance") || h.Contains("pkr");
                                var cellPar = new Paragraph(new Run(cellVal ?? ""))
                                {
                                    FontSize = 8.5,
                                    Margin = new Thickness(4, 4, 4, 4),
                                    FontWeight = (colIdx == 0 || isRightAlign) ? FontWeights.Bold : FontWeights.Normal,
                                    TextAlignment = isRightAlign ? TextAlignment.Right : TextAlignment.Left
                                };
                                row.Cells.Add(new TableCell(cellPar));
                                colIdx++;
                            }
                            rowGroup.Rows.Add(row);
                            rowIdx++;
                        }

                        if (totalsRow != null)
                        {
                            var totalRow = new TableRow { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)) };
                            int colIdx = 0;
                            foreach (var tVal in totalsRow)
                            {
                                var h = colIdx < headerList.Count ? headerList[colIdx].ToLower() : "";
                                bool isRightAlign = h.Contains("amount") || h.Contains("price") || h.Contains("rate") || h.Contains("val") || h.Contains("profit") || h.Contains("debit") || h.Contains("credit") || h.Contains("owed") || h.Contains("payable") || h.Contains("receivable") || h.Contains("advance") || h.Contains("pkr");
                                var cellPar = new Paragraph(new Run(tVal ?? ""))
                                {
                                    FontWeight = FontWeights.Bold,
                                    FontSize = 9,
                                    Margin = new Thickness(4, 6, 4, 6),
                                    TextAlignment = isRightAlign ? TextAlignment.Right : TextAlignment.Left
                                };
                                totalRow.Cells.Add(new TableCell(cellPar));
                                colIdx++;
                            }
                            rowGroup.Rows.Add(totalRow);
                        }

                        table.RowGroups.Add(rowGroup);
                        doc.Blocks.Add(table);
                    }

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, title);
                }
            });
        }

        public void PrintSalaryStaffRegister(System.Collections.Generic.IEnumerable<Staff> staffs, CompanySetting company)
        {
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    var printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        var staffList = System.Linq.Enumerable.ToList(staffs ?? System.Linq.Enumerable.Empty<Staff>());
                        decimal totalBasic = staffList.Sum(s => s.BasicSalary);

                        var doc = new FlowDocument
                        {
                            PageWidth = 794,  // Standard A4 Width (210mm @ 96 DPI)
                            PageHeight = 1123, // Standard A4 Height (297mm @ 96 DPI)
                            PagePadding = new Thickness(40),
                            ColumnWidth = 714,
                            FontFamily = new FontFamily("Times New Roman")
                        };

                        var compName = string.IsNullOrEmpty(company.CompanyName) ? "AL MADINA BUILDING MATERIAL ERP" : company.CompanyName.ToUpper();
                        var pHeader = new Paragraph(new Run(compName))
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.Maroon,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 4)
                        };
                        doc.Blocks.Add(pHeader);

                        var pSub = new Paragraph(new Run("SALARY STAFF REGISTER & PAYROLL STATEMENT"))
                        {
                            FontSize = 13,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        doc.Blocks.Add(pSub);

                        var pMeta = new Paragraph();
                        pMeta.Inlines.Add(new Run($"Total Staff Members: {staffList.Count}   |   Total Basic Salary: PKR {totalBasic:N2}\n") { FontWeight = FontWeights.Bold });
                        pMeta.Inlines.Add(new Run($"Statement Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Status: Active Payroll"));
                        pMeta.FontSize = 10;
                        pMeta.Margin = new Thickness(0, 0, 0, 14);
                        doc.Blocks.Add(pMeta);

                        var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0.5), BorderBrush = System.Windows.Media.Brushes.Gray };
                        table.Columns.Add(new TableColumn { Width = new GridLength(85) });   // CODE
                        table.Columns.Add(new TableColumn { Width = new GridLength(160) });  // STAFF NAME
                        table.Columns.Add(new TableColumn { Width = new GridLength(145) });  // DESIGNATION / DEPT
                        table.Columns.Add(new TableColumn { Width = new GridLength(105) });  // PHONE
                        table.Columns.Add(new TableColumn { Width = new GridLength(95) });   // JOINING DATE
                        table.Columns.Add(new TableColumn { Width = new GridLength(124) });  // BASIC SALARY (PKR)

                        var headerRowGroup = new TableRowGroup();
                        var headerRow = new TableRow { Background = System.Windows.Media.Brushes.DarkGreen };
                        string[] headers = { "CODE", "STAFF NAME", "DESIGNATION / DEPT", "PHONE", "JOINING DATE", "BASIC SALARY (PKR)" };
                        foreach (var h in headers)
                        {
                            var cell = new TableCell(new Paragraph(new Run(h))
                            {
                                FontSize = 9,
                                FontWeight = FontWeights.Bold,
                                Foreground = System.Windows.Media.Brushes.White,
                                TextAlignment = h.Contains("PKR") ? TextAlignment.Right : TextAlignment.Left
                            })
                            { Padding = new Thickness(4) };
                            headerRow.Cells.Add(cell);
                        }
                        headerRowGroup.Rows.Add(headerRow);
                        table.RowGroups.Add(headerRowGroup);

                        var dataRowGroup = new TableRowGroup();
                        bool alt = false;
                        foreach (var s in staffList)
                        {
                            var row = new TableRow
                            {
                                Background = alt ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White
                            };
                            alt = !alt;

                            var deptDesig = string.IsNullOrEmpty(s.Department) ? s.Designation ?? "-" : $"{s.Designation} ({s.Department})";

                            row.Cells.Add(new TableCell(new Paragraph(new Run(s.StaffCode ?? "-")) { FontSize = 9, FontWeight = FontWeights.Bold }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(s.FullName ?? "-")) { FontSize = 9, FontWeight = FontWeights.Bold }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(deptDesig)) { FontSize = 9 }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(s.Phone ?? "-")) { FontSize = 9 }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(s.JoiningDate.ToString("dd/MM/yyyy"))) { FontSize = 9 }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run($"{s.BasicSalary:N2}")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });

                            dataRowGroup.Rows.Add(row);
                        }

                        var totalRow = new TableRow { Background = System.Windows.Media.Brushes.LightGray };
                        totalRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontSize = 9, FontWeight = FontWeights.Bold }) { ColumnSpan = 5, Padding = new Thickness(4) });
                        totalRow.Cells.Add(new TableCell(new Paragraph(new Run($"{totalBasic:N2}")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });
                        dataRowGroup.Rows.Add(totalRow);

                        table.RowGroups.Add(dataRowGroup);
                        doc.Blocks.Add(table);

                        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                        printDialog.PrintDocument(paginator, "Salary Staff Register");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
        }

        public void PrintStaffLedger(Staff staff, System.Collections.Generic.IEnumerable<AlMadinaERP.Core.DTOs.SalaryLedgerRowDto> entries, CompanySetting company)
        {
            if (staff == null) return;
            company ??= new CompanySetting();

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    var printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        var entryList = System.Linq.Enumerable.ToList(entries ?? System.Linq.Enumerable.Empty<AlMadinaERP.Core.DTOs.SalaryLedgerRowDto>());
                        decimal totPaid = entryList.Sum(e => e.PaidOut);
                        decimal totAdv = entryList.Sum(e => e.AdvanceReceived);
                        decimal netBal = totPaid - totAdv;

                        var doc = new FlowDocument
                        {
                            PageWidth = 794,  // Standard A4 Width (210mm @ 96 DPI)
                            PageHeight = 1123, // Standard A4 Height (297mm @ 96 DPI)
                            PagePadding = new Thickness(40),
                            ColumnWidth = 714,
                            FontFamily = new FontFamily("Times New Roman")
                        };

                        var compName = string.IsNullOrEmpty(company.CompanyName) ? "AL MADINA BUILDING MATERIAL ERP" : company.CompanyName.ToUpper();
                        var pHeader = new Paragraph(new Run(compName))
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.Maroon,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 4)
                        };
                        doc.Blocks.Add(pHeader);

                        var pSub = new Paragraph(new Run($"STAFF SALARY STATEMENT - {(staff.FullName ?? "Staff").ToUpper()} ({staff.StaffCode ?? ""})"))
                        {
                            FontSize = 13,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        doc.Blocks.Add(pSub);

                        var pMeta = new Paragraph();
                        pMeta.Inlines.Add(new Run($"Phone: {staff.Phone ?? "N/A"}   |   Designation: {staff.Designation ?? "N/A"}   |   Basic Salary: PKR {staff.BasicSalary:N2}\n") { FontWeight = FontWeights.Bold });
                        pMeta.Inlines.Add(new Run($"Statement Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Entries: {entryList.Count}"));
                        pMeta.FontSize = 10;
                        pMeta.Margin = new Thickness(0, 0, 0, 14);
                        doc.Blocks.Add(pMeta);

                        var table = new Table { CellSpacing = 0, BorderThickness = new Thickness(0.5), BorderBrush = System.Windows.Media.Brushes.Gray };
                        table.Columns.Add(new TableColumn { Width = new GridLength(80) });  // DATE
                        table.Columns.Add(new TableColumn { Width = new GridLength(90) });  // VOUCHER #
                        table.Columns.Add(new TableColumn { Width = new GridLength(100) }); // TYPE
                        table.Columns.Add(new TableColumn { Width = new GridLength(180) }); // DESCRIPTION
                        table.Columns.Add(new TableColumn { Width = new GridLength(85) });  // DEBIT (PKR)
                        table.Columns.Add(new TableColumn { Width = new GridLength(85) });  // CREDIT (PKR)
                        table.Columns.Add(new TableColumn { Width = new GridLength(94) });  // BALANCE (PKR)

                        var headerRowGroup = new TableRowGroup();
                        var headerRow = new TableRow { Background = System.Windows.Media.Brushes.DarkGreen };
                        string[] headers = { "DATE", "VOUCHER #", "TYPE", "DESCRIPTION", "DEBIT (PKR)", "CREDIT (PKR)", "BALANCE (PKR)" };
                        foreach (var h in headers)
                        {
                            var cell = new TableCell(new Paragraph(new Run(h))
                            {
                                FontSize = 9,
                                FontWeight = FontWeights.Bold,
                                Foreground = System.Windows.Media.Brushes.White,
                                TextAlignment = h.Contains("PKR") ? TextAlignment.Right : TextAlignment.Left
                            })
                            { Padding = new Thickness(4) };
                            headerRow.Cells.Add(cell);
                        }
                        headerRowGroup.Rows.Add(headerRow);
                        table.RowGroups.Add(headerRowGroup);

                        var dataRowGroup = new TableRowGroup();
                        bool alt = false;
                        decimal runningBalance = 0m;
                        foreach (var entry in entryList)
                        {
                            var row = new TableRow
                            {
                                Background = alt ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White
                            };
                            alt = !alt;

                            runningBalance += entry.PaidOut - entry.AdvanceReceived;
                            var vNum = string.IsNullOrEmpty(entry.VoucherNumber) ? $"VCH-SAL-{entry.Date:yyyyMMdd}" : entry.VoucherNumber;

                            row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Date.ToString("dd/MM/yyyy"))) { FontSize = 9 }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(vNum)) { FontSize = 9, FontWeight = FontWeights.Bold }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Type ?? "-")) { FontSize = 9 }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Description ?? "-")) { FontSize = 9 }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(entry.PaidOut > 0 ? $"{entry.PaidOut:N2}" : "-")) { FontSize = 9, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run(entry.AdvanceReceived > 0 ? $"{entry.AdvanceReceived:N2}" : "-")) { FontSize = 9, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });
                            row.Cells.Add(new TableCell(new Paragraph(new Run($"{runningBalance:N2}")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });

                            dataRowGroup.Rows.Add(row);
                        }

                        var totalRow = new TableRow { Background = System.Windows.Media.Brushes.LightGray };
                        totalRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontSize = 9, FontWeight = FontWeights.Bold }) { ColumnSpan = 4, Padding = new Thickness(4) });
                        totalRow.Cells.Add(new TableCell(new Paragraph(new Run($"{totPaid:N2}")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });
                        totalRow.Cells.Add(new TableCell(new Paragraph(new Run($"{totAdv:N2}")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });
                        totalRow.Cells.Add(new TableCell(new Paragraph(new Run($"{runningBalance:N2}")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }) { Padding = new Thickness(4) });
                        dataRowGroup.Rows.Add(totalRow);

                        table.RowGroups.Add(dataRowGroup);
                        doc.Blocks.Add(table);

                        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                        printDialog.PrintDocument(paginator, $"Staff Salary Ledger - {staff.FullName}");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
        }
    }
}


