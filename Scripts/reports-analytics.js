/* ==========================================================================
   UniShare - Reports & Analytics Chart.js Controller
   ========================================================================== */

$(document).ready(function () {
    // Design System Color Palette
    var colors = {
        indigo: '#4F46E5',         // Vibrant Indigo (Primary)
        indigoLight: 'rgba(79, 70, 229, 0.15)',
        cyan: '#06B6D4',           // Cyan
        emerald: '#10B981',        // Emerald Green
        emeraldLight: 'rgba(16, 185, 129, 0.15)',
        amber: '#F59E0B',          // Amber
        rose: '#F43F5E',           // Rose Red
        purple: '#8B5CF6',         // Purple
        gridBorder: '#E2E8F0',
        textMuted: '#94A3B8',
        textDark: '#0F172A'
    };

    // Chart Global Defaults for Inter Font
    if (window.Chart) {
        Chart.defaults.font.family = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif";
        Chart.defaults.font.size = 11;
        Chart.defaults.color = colors.textMuted;
        Chart.defaults.plugins.tooltip.padding = 10;
        Chart.defaults.plugins.tooltip.cornerRadius = 8;
        Chart.defaults.plugins.tooltip.backgroundColor = '#0F172A';
        Chart.defaults.plugins.tooltip.titleColor = '#FFFFFF';
        Chart.defaults.plugins.tooltip.bodyColor = '#CBD5E1';
    }

    var chartRentalActivity = null;
    var chartRevenueFlow = null;
    var chartCategories = null;

    // Helper: Initialize Chart.js Instances
    function initCharts(data) {
        // 1. Rental & Booking Activity Trends (Bar Chart: Checkouts vs Returns)
        var ctxActivity = document.getElementById('rentalActivityChart');
        if (ctxActivity) {
            chartRentalActivity = new Chart(ctxActivity, {
                type: 'bar',
                data: {
                    labels: data.activity.labels,
                    datasets: [
                        {
                            label: 'Checkouts',
                            data: data.activity.checkouts,
                            backgroundColor: colors.indigo,
                            borderRadius: 6,
                            maxBarThickness: 32
                        },
                        {
                            label: 'Returns',
                            data: data.activity.returns,
                            backgroundColor: colors.emerald,
                            borderRadius: 6,
                            maxBarThickness: 32
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
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
                                font: { weight: '600' }
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

        // 2. Revenue & Cash Flow Breakdown (Line Chart: GCash vs Cash)
        var ctxRevenue = document.getElementById('revenueFlowChart');
        if (ctxRevenue) {
            chartRevenueFlow = new Chart(ctxRevenue, {
                type: 'line',
                data: {
                    labels: data.revenue.labels,
                    datasets: [
                        {
                            label: 'GCash Payments',
                            data: data.revenue.gcash,
                            borderColor: colors.indigo,
                            backgroundColor: colors.indigoLight,
                            fill: true,
                            tension: 0.35,
                            borderWidth: 2.5,
                            pointRadius: 4,
                            pointHoverRadius: 6,
                            pointBackgroundColor: colors.indigo
                        },
                        {
                            label: 'Cash Payments',
                            data: data.revenue.cash,
                            borderColor: colors.emerald,
                            backgroundColor: colors.emeraldLight,
                            fill: true,
                            tension: 0.35,
                            borderWidth: 2,
                            pointRadius: 3,
                            pointHoverRadius: 5,
                            pointBackgroundColor: colors.emerald
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
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
                                font: { weight: '600' }
                            }
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    var val = context.parsed.y || 0;
                                    return context.dataset.label + ': PHP ' + val.toLocaleString('en-US', { minimumFractionDigits: 2 });
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
                                callback: function (value) {
                                    return 'PHP ' + (value >= 1000 ? (value / 1000) + 'k' : value);
                                }
                            }
                        }
                    }
                }
            });
        }

        // 3. Equipment Categories Performance (Horizontal Bar Chart)
        var ctxCategories = document.getElementById('categoriesChart');
        if (ctxCategories) {
            chartCategories = new Chart(ctxCategories, {
                type: 'bar',
                data: {
                    labels: data.categories.labels,
                    datasets: [
                        {
                            label: 'Active Bookings',
                            data: data.categories.data,
                            backgroundColor: [
                                colors.indigo,
                                colors.purple,
                                colors.cyan,
                                colors.amber,
                                colors.rose
                            ],
                            borderRadius: 6,
                            maxBarThickness: 18
                        }
                    ]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false }
                    },
                    scales: {
                        x: {
                            border: { dash: [4, 4] },
                            grid: { color: colors.gridBorder },
                            ticks: { color: colors.textMuted }
                        },
                        y: {
                            grid: { display: false },
                            ticks: { color: colors.textDark, font: { weight: '600' } }
                        }
                    }
                }
            });
        }
    }

    // AJAX Function: Fetch data from controller and update Chart.js datasets dynamically
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

                if (!chartRentalActivity) {
                    initCharts(res);
                } else {
                    // Update Activity Trends (Checkouts vs Returns)
                    chartRentalActivity.data.labels = res.activity.labels;
                    chartRentalActivity.data.datasets[0].data = res.activity.checkouts;
                    chartRentalActivity.data.datasets[1].data = res.activity.returns;
                    chartRentalActivity.update();

                    // Update Revenue Breakdown (GCash vs Cash)
                    chartRevenueFlow.data.labels = res.revenue.labels;
                    chartRevenueFlow.data.datasets[0].data = res.revenue.gcash;
                    chartRevenueFlow.data.datasets[1].data = res.revenue.cash;
                    chartRevenueFlow.update();

                    // Update Equipment Categories
                    chartCategories.data.labels = res.categories.labels;
                    chartCategories.data.datasets[0].data = res.categories.data;
                    chartCategories.update();
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