/* ==========================================================================
   UniShare - Reports & Analytics Chart.js Controller
   4 Overhauled Visualizations:
     1. Rental Transactions Over Time (Line Chart)
     2. Item Listing Status Distribution (Donut Chart)
     3. Most Rented Items (Horizontal Bar Chart)
     4. User Activity Overview (Grouped Bar Chart)
   ========================================================================== */

$(document).ready(function () {
    // Design System Color Tokens (matching design.md & site.css)
    var colors = {
        indigo: '#4F46E5',          // Primary Indigo
        indigoLight: 'rgba(79, 70, 229, 0.12)',
        emerald: '#10B981',         // Success / Approved Emerald
        emeraldLight: 'rgba(16, 185, 129, 0.12)',
        cyan: '#06B6D4',            // Accent Cyan
        cyanLight: 'rgba(6, 182, 212, 0.12)',
        purple: '#8B5CF6',          // Purple
        amber: '#F59E0B',           // Warning / Pending Amber
        rose: '#EF4444',            // Danger / Rejected Rose
        gridBorder: '#F1F5F9',
        textMuted: '#94A3B8',
        textDark: '#0F172A',
        tooltipBg: '#0F172A'
    };

    // Configure Chart.js Global Defaults
    if (window.Chart) {
        Chart.defaults.font.family = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif";
        Chart.defaults.font.size = 11;
        Chart.defaults.color = colors.textMuted;
        Chart.defaults.plugins.tooltip.padding = 12;
        Chart.defaults.plugins.tooltip.cornerRadius = 8;
        Chart.defaults.plugins.tooltip.backgroundColor = colors.tooltipBg;
        Chart.defaults.plugins.tooltip.titleColor = '#FFFFFF';
        Chart.defaults.plugins.tooltip.titleFont = { size: 12, weight: '700' };
        Chart.defaults.plugins.tooltip.bodyColor = '#CBD5E1';
        Chart.defaults.plugins.tooltip.bodyFont = { size: 11, weight: '500' };
        Chart.defaults.plugins.tooltip.footerColor = '#94A3B8';
        Chart.defaults.plugins.tooltip.footerFont = { size: 10, style: 'italic' };
        Chart.defaults.plugins.tooltip.borderWidth = 1;
        Chart.defaults.plugins.tooltip.borderColor = '#334155';
    }

    // Chart Instance Handles
    var chartRentalTransactions = null;
    var chartItemListingStatus = null;
    var chartMostRentedItems = null;
    var chartUserActivity = null;

    // Helper: Initialize all 4 Chart.js Instances with Complete Hover Tooltips
    function initCharts(data, currentTimeframe) {
        currentTimeframe = currentTimeframe || 'months';

        // ------------------------------------------------------------------
        // 1. Rental Transactions Over Time (Line Chart)
        // ------------------------------------------------------------------
        var ctxLine = document.getElementById('rentalTransactionsChart');
        if (ctxLine) {
            chartRentalTransactions = new Chart(ctxLine, {
                type: 'line',
                data: {
                    labels: data.rentalTransactions.labels,
                    datasets: [
                        {
                            label: 'Rental Transactions',
                            data: data.rentalTransactions.data,
                            borderColor: colors.indigo,
                            backgroundColor: colors.indigoLight,
                            fill: true,
                            tension: 0.35,
                            borderWidth: 2.5,
                            pointRadius: 4,
                            pointHoverRadius: 7,
                            pointBackgroundColor: colors.indigo,
                            pointBorderColor: '#FFFFFF',
                            pointBorderWidth: 2
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: {
                        mode: 'index',
                        intersect: false
                    },
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            callbacks: {
                                title: function (tooltipItems) {
                                    return 'Period: ' + tooltipItems[0].label;
                                },
                                label: function (context) {
                                    var val = context.parsed.y || 0;
                                    return ' ' + context.dataset.label + ': ' + val.toLocaleString() + ' completed rentals';
                                },
                                afterLabel: function () {
                                    return ' Verified QCU Handover Log';
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            grid: { display: false },
                            ticks: { color: colors.textMuted, font: { weight: '600' } }
                        },
                        y: {
                            border: { dash: [4, 4] },
                            grid: { color: colors.gridBorder },
                            ticks: {
                                color: colors.textMuted,
                                precision: 0,
                                callback: function (val) {
                                    return val + ' rentals';
                                }
                            }
                        }
                    }
                }
            });
        }

        // ------------------------------------------------------------------
        // 2. Item Listing Status Distribution (Donut Chart)
        // ------------------------------------------------------------------
        var ctxDonut = document.getElementById('itemListingStatusChart');
        if (ctxDonut) {
            chartItemListingStatus = new Chart(ctxDonut, {
                type: 'doughnut',
                data: {
                    labels: data.itemListingStatus.labels,
                    datasets: [
                        {
                            data: data.itemListingStatus.data,
                            backgroundColor: [
                                colors.emerald, // Approved
                                colors.amber,   // Pending
                                colors.rose     // Rejected
                            ],
                            borderColor: '#FFFFFF',
                            borderWidth: 2,
                            hoverOffset: 6
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '68%',
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                boxWidth: 12,
                                boxHeight: 12,
                                usePointStyle: true,
                                pointStyle: 'circle',
                                color: colors.textDark,
                                font: { weight: '600', size: 11 },
                                padding: 14
                            }
                        },
                        tooltip: {
                            callbacks: {
                                title: function (tooltipItems) {
                                    return 'Listing Status: ' + tooltipItems[0].label;
                                },
                                label: function (context) {
                                    var val = context.parsed || 0;
                                    var dataArr = context.dataset.data || [];
                                    var total = dataArr.reduce(function (a, b) { return a + b; }, 0);
                                    var pct = total > 0 ? ((val / total) * 100).toFixed(1) : '0.0';
                                    return ' ' + context.label + ': ' + val.toLocaleString() + ' items (' + pct + '%)';
                                },
                                afterLabel: function (context) {
                                    if (context.label === 'Approved') return ' Active in campus catalog';
                                    if (context.label === 'Pending') return ' Awaiting admin verification';
                                    return ' Declined due to policy non-compliance';
                                }
                            }
                        }
                    }
                }
            });
        }

        // ------------------------------------------------------------------
        // 3. Most Rented Items (Horizontal Bar Chart)
        // ------------------------------------------------------------------
        var ctxBarH = document.getElementById('mostRentedItemsChart');
        if (ctxBarH) {
            chartMostRentedItems = new Chart(ctxBarH, {
                type: 'bar',
                data: {
                    labels: data.mostRentedItems.labels,
                    datasets: [
                        {
                            label: 'Total Bookings',
                            data: data.mostRentedItems.data,
                            backgroundColor: [
                                colors.indigo,
                                colors.purple,
                                colors.cyan,
                                colors.emerald,
                                colors.amber
                            ],
                            borderRadius: 6,
                            maxBarThickness: 20
                        }
                    ]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            callbacks: {
                                title: function (tooltipItems) {
                                    return 'Equipment: ' + tooltipItems[0].label;
                                },
                                label: function (context) {
                                    var val = context.parsed.x || 0;
                                    return ' Volume: ' + val.toLocaleString() + ' verified student rentals';
                                },
                                afterLabel: function () {
                                    return ' High demand academic equipment';
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            border: { dash: [4, 4] },
                            grid: { color: colors.gridBorder },
                            ticks: {
                                color: colors.textMuted,
                                precision: 0
                            }
                        },
                        y: {
                            grid: { display: false },
                            ticks: {
                                color: colors.textDark,
                                font: { weight: '600' }
                            }
                        }
                    }
                }
            });
        }

        // ------------------------------------------------------------------
        // 4. User Activity Overview (Grouped Bar Chart)
        // ------------------------------------------------------------------
        var ctxBarGroup = document.getElementById('userActivityChart');
        if (ctxBarGroup) {
            chartUserActivity = new Chart(ctxBarGroup, {
                type: 'bar',
                data: {
                    labels: data.userActivity.labels,
                    datasets: [
                        {
                            label: 'Active Users',
                            data: data.userActivity.activeUsers,
                            backgroundColor: colors.indigo,
                            borderRadius: 4,
                            maxBarThickness: 14
                        },
                        {
                            label: 'Renting Transactions',
                            data: data.userActivity.rentingTransactions,
                            backgroundColor: colors.emerald,
                            borderRadius: 4,
                            maxBarThickness: 14
                        },
                        {
                            label: 'Borrowing Transactions',
                            data: data.userActivity.borrowingTransactions,
                            backgroundColor: colors.cyan,
                            borderRadius: 4,
                            maxBarThickness: 14
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: {
                        mode: 'index',
                        intersect: false
                    },
                    plugins: {
                        legend: {
                            position: 'top',
                            align: 'end',
                            labels: {
                                boxWidth: 10,
                                boxHeight: 10,
                                usePointStyle: true,
                                pointStyle: 'circle',
                                color: colors.textDark,
                                font: { weight: '600', size: 11 }
                            }
                        },
                        tooltip: {
                            callbacks: {
                                title: function (tooltipItems) {
                                    return 'Timeframe: ' + tooltipItems[0].label;
                                },
                                label: function (context) {
                                    var val = context.parsed.y || 0;
                                    return ' ' + context.dataset.label + ': ' + val.toLocaleString() + ' entries';
                                },
                                afterLabel: function () {
                                    return ' Audited QCU Portal Interaction';
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            grid: { display: false },
                            ticks: { color: colors.textMuted, font: { weight: '600' } }
                        },
                        y: {
                            border: { dash: [4, 4] },
                            grid: { color: colors.gridBorder },
                            ticks: {
                                color: colors.textMuted,
                                precision: 0
                            }
                        }
                    }
                }
            });
        }
    }

    // Helper: AJAX fetch for timeframe and smooth dynamic chart updates
    function updateCharts(timeframe) {
        var $activeBtn = $('.btn-timeframe[data-timeframe="' + timeframe + '"]');
        $('.btn-timeframe').removeClass('active');
        $activeBtn.addClass('active');

        $.ajax({
            url: '/ReportsAnalytics/GetChartData',
            type: 'GET',
            data: { timeframe: timeframe },
            dataType: 'json',
            success: function (res) {
                if (!res || !res.success) {
                    return;
                }

                if (!chartRentalTransactions) {
                    initCharts(res, timeframe);
                } else {
                    // 1. Update Rental Transactions Over Time
                    chartRentalTransactions.data.labels = res.rentalTransactions.labels;
                    chartRentalTransactions.data.datasets[0].data = res.rentalTransactions.data;
                    chartRentalTransactions.update();

                    // 2. Update Item Listing Status Distribution
                    chartItemListingStatus.data.labels = res.itemListingStatus.labels;
                    chartItemListingStatus.data.datasets[0].data = res.itemListingStatus.data;
                    chartItemListingStatus.update();

                    // 3. Update Most Rented Items
                    chartMostRentedItems.data.labels = res.mostRentedItems.labels;
                    chartMostRentedItems.data.datasets[0].data = res.mostRentedItems.data;
                    chartMostRentedItems.update();

                    // 4. Update User Activity Overview
                    chartUserActivity.data.labels = res.userActivity.labels;
                    chartUserActivity.data.datasets[0].data = res.userActivity.activeUsers;
                    chartUserActivity.data.datasets[1].data = res.userActivity.rentingTransactions;
                    chartUserActivity.data.datasets[2].data = res.userActivity.borrowingTransactions;
                    chartUserActivity.update();
                }
            },
            error: function (xhr, status, error) {
                console.error('Error fetching analytics chart data:', error);
            }
        });
    }

    // Event Listener: Months / Weeks / Days Filter Buttons
    $(document).on('click', '.btn-timeframe', function (e) {
        e.preventDefault();
        var selectedTimeframe = $(this).data('timeframe');
        updateCharts(selectedTimeframe);
    });

    // Initial Load: Default to "months"
    updateCharts('months');
});
