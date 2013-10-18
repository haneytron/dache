$(document).ready(function () {
    $(".accordion").accordion();
    $(".tabs").tabs();
    
    // Hover states on the static widgets
    $(".cacheHostDetailsButton, #icons li").hover(
        function () {
            $(this).addClass("ui-state-hover");
        },
        function () {
            $(this).removeClass("ui-state-hover");
        }
    );

    // Wire up for the window resize event
    function applyHeightToPanel2() {
        var maxHeight = $(window).height() - 150;
        //var panel1Height = $("#panel1").height();
        //var panel3Height = $("#panel3").height();
        //maxHeight = panel1Height > maxHeight ? panel1Height : maxHeight;
        //maxHeight = panel3Height > maxHeight ? panel3Height : maxHeight;
        $("#panel2").css({ "height": (maxHeight) + "px" });
    }

    $(window).resize(applyHeightToPanel2);

    // Set height of panel2
    applyHeightToPanel2();
    // Catches dynamic redraw delay
    //window.setTimeout(applyHeightToPanel2, 50); // This sucks, need a better way

    // TEST div creation
    var cacheHost = new CacheHost("test$%^", $("#performanceTab"), $("#healthTab"));
    //cacheHost.showData();
});

/********************************************************************************
* Creates a Pie Chart
********************************************************************************/
function PieChart(data, width, height, domObject) {
    var data = data;

    // Only do DOM add when ready
    $(document).ready(function () {
        initialize(data, width, height, domObject);
    });

    function initialize(data, width, height, domObject) {
        // TODO: prototype the constants. see: http://www.phpied.com/3-ways-to-define-a-javascript-class/
        var π = Math.PI,
        radians = π / 180,
        degrees = 180 / π,
        a;

        // TODO: adjust the 35 part as size changes
        radius = (width - 35) / 2;

        var colorArray = ["#8a56e2", "#cf56e2", "#e256ae", "#e25668", "#e28956", "#e2cf56", "#aee256", "#68e256", "#56e289", "#56e2cf", "#56aee2", "#5668e2"];

        var color = d3.scale.ordinal()
            .range(colorArray);

        var arc = d3.svg.arc()
            .outerRadius(radius - 10)
            .innerRadius(0);

        var pie = d3.layout.pie()
            .sort(null)
            .value(function (d) { return d.population; });

        var svg = d3.select(domObject).append("svg")
            .attr("width", width)
            .attr("height", height)
            .append("g")
            .attr("transform", "translate(" + width / 2 + "," + height / 2 + ")");

        data.forEach(function (d) {
            d.population = +d.population;
        });

        var g = svg.selectAll(".arc")
            .data(pie(data))
            .enter().append("g")
            .attr("class", "arc")
            .attr("title", function (d) { return d.data.age; });

        g.append("path")
            .attr("d", arc)
            .style("fill", "#DDDDDD")
            .transition().duration(1000)
            .style("fill", function (d) { return color(d.data.age); });

        //g.append("text")
        //    .attr("transform", function (d) { return "translate(" + edge(d) + ") " + "rotate(" + (a = angle(d), (a > 90 ? a - 180 : a)) + ")"; })
        //    .attr("dy", ".35em")
        //    .style("text-anchor", function (d) { return angle(d) > 90 ? "end" : "start"; })
        //    .text(function (d) { return d.data.age; });

        function edge(d, i) {
            var r = radius,
                a = (d.startAngle + d.endAngle) / 2 - π / 2;
            return [Math.cos(a) * r, Math.sin(a) * r];
        };

        function angle(d, i) {
            var a = degrees * ((d.startAngle + d.endAngle) / 2 - π / 2);
            return a;
        };

        // Tooltip
        $(domObject + " .arc").tooltip();
    }
}

/********************************************************************************
* Creates a Line Chart that updates every time a point is added
********************************************************************************/
function RealtimeLineChart(width, height, domain, interpolation, domObject) {
    var chartData = null;
    var x = null;
    var line = null;
    var path = null;
    var xOffset = 0;

    var random = d3.random.normal(2500, 800);

    this.addPoint = function (value) {
        // push a new data point onto the back
        chartData.push(random());

        xOffset--;

        // redraw the line, and then slide it to the left
        path
            .attr("d", line)
            .transition()
            .duration(globalRefreshIntervalMilliseconds)
            .ease("linear")
            .attr("transform", "translate(" + x(xOffset) + ")");
        // pop the old data point off the front
        //chartData.shift();
    }

    // Only do DOM add when ready
    $(document).ready(function () {
        initialize(width, height, domain, interpolation, domObject);
    });

    function initialize(width, height, domain, interpolation, domObject) {
        chartData = d3.range(domain[1] + 1).map(random);

        //var margin = { top: 6, right: 0, bottom: 6, left: 20 /* was 40 */ },
        var margin = { top: 6, right: 0, bottom: 6, left: 80 /* was 40 */ },
            width = width - margin.right,
            height = height - margin.top - margin.bottom;

        x = d3.scale.linear()
            .domain(domain)
            .range([0, width]);

        var y = d3.scale.linear()
            .domain([0, /* TODO: derive this a better way */5000])
            .range([height, 0]);

        line = d3.svg.line()
            .interpolate(interpolation)
            .x(function (d, i) { return x(i); })
            .y(function (d, i) { return y(d); });

        var svg = d3.select(domObject).append("svg")
            .attr("width", width + margin.left + margin.right)
            .attr("height", height + margin.top + margin.bottom)
            .style("margin-left", -margin.left / 2 + "px")
            .append("g")
            .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

        svg.append("defs").append("clipPath")
            .attr("id", "clip")
            .append("rect")
            .attr("width", 0)
            .transition().duration(1000)
            .attr("width", width)
            .attr("height", height);

        svg.append("g")
            .attr("class", "y axis")
            //.append("text")
            //.attr("transform", "rotate(-90)")
            //.attr("y", 6)
            //.attr("dy", ".71em")
            //.style("text-anchor", "end")
            //.text("Number")
            .call(d3.svg.axis().scale(y).ticks(5).orient("left"));

        path = svg.append("g")
            .attr("clip-path", "url(#clip)")
            .append("path")
            .data([chartData])
            .attr("class", "line")
            .attr("d", line);
    }
}

/********************************************************************************
* Creates a Stacked Bar Chart that updates every time a point is added
********************************************************************************/
function StackedBarChart(width, height, domObject) {
    var data = [{ "CacheHost": "server1.ff.p10", "Object Count": 222222, "Ops / Sec": 444444, "Evictions / Sec": 111111 },
                { "CacheHost": "server2.ff.p10", "Object Count": 444444, "Ops / Sec": 555555, "Evictions / Sec": 222222 },
                { "CacheHost": "server3.ff.p10", "Object Count": 555555, "Ops / Sec": 333333, "Evictions / Sec": 111111 },
                { "CacheHost": "server4.ff.p10", "Object Count": 333333, "Ops / Sec": 222222, "Evictions / Sec": 222222 },
                { "CacheHost": "server5.ff.p10", "Object Count": 333333, "Ops / Sec": 222222, "Evictions / Sec": 222222 },
                { "CacheHost": "server6.ff.p10", "Object Count": 333333, "Ops / Sec": 222222, "Evictions / Sec": 222222 },
                { "CacheHost": "server7.ff.p10", "Object Count": 333333, "Ops / Sec": 222222, "Evictions / Sec": 222222 },
                { "CacheHost": "server8.ff.p10", "Object Count": 666666, "Ops / Sec": 666666, "Evictions / Sec": 222222 }];

    var dataKey = "CacheHost";
    var width = width;
    var height = height;
    var domObject = domObject;

    // Only do DOM add when ready
    $(document).ready(function () {
        initialize(dataKey, width, height, domObject, true);
    });

    this.addData = function (newData) {
        data.push(newData);

        initialize(dataKey, width, height, domObject, false);
    }

    function initialize(dataKey, width, height, domObject, doIntroTransition) {
        var margin = { top: 20, right: 100, bottom: 20, left: 60 },
            width = width - margin.left - margin.right,
            height = width - margin.top - margin.bottom;

        var x = d3.scale.ordinal()
            .rangeRoundBands([0, width], .1);

        var y = d3.scale.linear()
            .rangeRound([height, 0]);

        var colorArray = ["#56e2cf", "#56aee2", "#5668e2"];

        var color = d3.scale.ordinal()
            //.range(["#98abc5", "#8a89a6", "#7b6888", "#6b486b", "#a05d56", "#d0743c", "#ff8c00"]);
            .range(colorArray);

        var xAxis = d3.svg.axis()
            .scale(x)
            .orient("bottom");

        var yAxis = d3.svg.axis()
            .scale(y)
            .orient("left")
            .tickFormat(d3.format(".0%"));

        // Clear the domObject
        $(domObject).empty();

        var svg = d3.select(domObject).append("svg")
            .attr("width", width + margin.left + margin.right)
            .attr("height", height + margin.top + margin.bottom)
            .attr("class", "stackedBarChart")
            .append("g")
            .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

        color.domain(d3.keys(data[0]).filter(function (key) { return key !== dataKey && key !== "measuredValues"; }));

        data.forEach(function (d) {
            var y0 = 0;
            d.measuredValues = color.domain().map(function (name) { return { name: name, y0: y0, y1: y0 += +d[name] }; });
            d.measuredValues.forEach(function (d) { d.y0 /= y0; d.y1 /= y0; });
        });

        data.sort(function (a, b) { return b.measuredValues[0].y1 - a.measuredValues[0].y1; });

        x.domain(data.map(function (d) { return d.CacheHost; }));

        //svg.append("g")
        //    .attr("class", "x axis")
        //    .attr("transform", "translate(0," + height + ")")
        //    .call(xAxis);

        svg.append("g")
            .attr("class", "y axis")
            .call(yAxis);

        var state = svg.selectAll(".measuredValues")
            .data(data)
            .enter().append("g")
            .attr("class", "measuredValue")
            .attr("transform", function (d) { return "translate(" + x(d.CacheHost) + ",0)"; })
            // For tooltip
            .attr("title", function (d) { return d.CacheHost });

        if (doIntroTransition) {
            state.selectAll("rect")
                .data(function (d) { return d.measuredValues; })
                .enter().append("rect")
                .attr("width", x.rangeBand())
                .attr("y", -1000)
                .transition().duration(1000)
                .attr("y", function (d) { return y(d.y1); })
                .attr("height", function (d) { return y(d.y0) - y(d.y1); })
                .style("fill", function (d) { return color(d.name); });
        }
        else {
            state.selectAll("rect")
                .data(function (d) { return d.measuredValues; })
                .enter().append("rect")
                .attr("width", x.rangeBand())
                .attr("y", function (d) { return y(d.y1); })
                .attr("height", function (d) { return y(d.y0) - y(d.y1); })
                .style("fill", function (d) { return color(d.name); });
        }

        var legend = svg.select(".measuredValue:last-child").selectAll(".legend")
            .data(function (d) { return d.measuredValues; })
            .enter().append("g")
            .attr("class", "legend")
            .attr("transform", function (d) { return "translate(" + x.rangeBand() / 2 + "," + y((d.y0 + d.y1) / 2) + ")"; });

        //legend.append("line")
        //    .attr("x2", 10);

        legend.append("text")
            .attr("x", 28)
            .attr("dy", ".35em")
            .text(function (d) { return "<- " + d.name; });

        // Tooltip
        $(domObject + " .measuredValue").tooltip();
    }
}

/********************************************************************************
* Creates the DOM objects for the Performance tab
********************************************************************************/
function BuildPerformanceTab(cacheHostName, cacheHostNameForDivIds) {
    // Performance Tab Div
    var div = $("<div></div>");
    // Title
    $("<div></div>", {
        html: cacheHostName + " Performance"
    }).addClass("title").appendTo(div);
    // Adds per sec
    var widgetWrapperDiv = createSmallWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Adds / Sec"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "AddsPerSecondChart").appendTo(widgetWrapperDiv);
    // Gets per sec
    widgetWrapperDiv = createSmallWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Gets / Sec"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "GetsPerSecondChart").appendTo(widgetWrapperDiv);
    // Removes per sec
    widgetWrapperDiv = createSmallWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Removes / Sec"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "RemovesPerSecondChart").appendTo(widgetWrapperDiv);
    // Evictions per sec
    widgetWrapperDiv = createSmallWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Evictions / Sec"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "EvictionsPerSecondChart").appendTo(widgetWrapperDiv);
    // Memory usage %
    widgetWrapperDiv = createSmallWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Memory Usage %"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "MemoryUsagePercentChart").appendTo(widgetWrapperDiv);
    // Memory usage
    widgetWrapperDiv = createSmallWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Total Memory Usage (MB)"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "MemoryUsageChart").appendTo(widgetWrapperDiv);

    // Return the wrapping element
    return div;
}

/********************************************************************************
* Creates the DOM objects for the Health tab
********************************************************************************/
function BuildHealthTab(cacheHostName, cacheHostNameForDivIds) {
    // Health Tab Div
    var div = $("<div></div>");
    // Title
    $("<div></div>", {
        html: cacheHostName + " Health"
    }).addClass("title").appendTo(div);
    // Object count vs operations
    var widgetWrapperDiv = createMediumWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Object Count vs Operations"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "ObjectCountVsOperationsStackedBarChart").appendTo(widgetWrapperDiv);
    // Adds vs gets vs removes
    widgetWrapperDiv = createMediumWidgetDiv();
    widgetWrapperDiv.appendTo(div);
    $("<div></div>", {
        html: "Adds vs Gets vs Removes"
    }).appendTo(widgetWrapperDiv);
    $("<div></div>").attr("id", cacheHostNameForDivIds + "AddsVsGetsVsRemovesStackedBarChart").appendTo(widgetWrapperDiv);

    // Return the wrapping element
    return div;
}

function createSmallWidgetDiv() {
    return $("<div></div>").addClass("ui-helper-reset ui-widget-content ui-corner-top ui-corner-bottom inlineWidgetSmall");
}

function createMediumWidgetDiv() {
    return $("<div></div>").addClass("ui-helper-reset ui-widget-content ui-corner-top ui-corner-bottom inlineWidgetMedium");
}

//$(document).ready(function () {
//    // Call out to the Beer List Web API
//    $.ajax({
//        type: "GET",
//        url: "/ajax/beerlist",
//        accepts: "application/json",
//        success: function (msg) {
//            $.each(msg, function () {
//                // TR element
//                var tr = $("<tr/>");
//                tr.appendTo($("#beerListTable"));

//                // Name
//                $("<td/>", {
//                    html: this.Name
//                }).addClass("borderBottom").attr("valign", "top").attr("width", "25%").appendTo(tr);
//                // Manufacturer
//                $("<td/>", {
//                    className: 'borderBottom',
//                    html: this.Manufacturer
//                }).addClass("borderBottom").attr("valign", "top").attr("width", "25%").appendTo(tr);
//                // Description
//                $("<td/>", {
//                    className: 'borderBottom',
//                    html: this.Description
//                }).addClass("borderBottom").attr("valign", "top").attr("width", "40%").appendTo(tr);
//                // Rating
//                $("<td/>", {
//                    className: 'borderBottom',
//                    html: this.Rating
//                }).addClass("borderBottom").attr("valign", "top").attr("align", "center").attr("width", "10%").appendTo(tr);
//            });

//            // Update when
//            var date = new Date();
//            $("#when").text((date.getMonth() + 1) + "/" + date.getDate() + "/" + date.getFullYear());
//        },
//        error: function (msg) {
//            alert("It broke!");
//        }
//    });
//});

/********************************************************************************
* Creates a Cache Host object and encompasses all related DOM objects
********************************************************************************/
function CacheHost(cacheHostName, performanceTabElement, healthTabElement) {
    // Local variables
    var _cacheHostName = cacheHostName;
    var _cacheHostNameForDivIds = cacheHostName.replace(/[^a-zA-Z0-9-_\s]/g, ""); // remove illegal div ID characters
    var _performanceTabElement = performanceTabElement;
    var _healthTabElement = healthTabElement;
    var _performanceTab = BuildPerformanceTab(_cacheHostName, _cacheHostNameForDivIds);
    var _healthTab = BuildHealthTab(_cacheHostName, _cacheHostNameForDivIds);

    // Only do DOM add when ready
    $(document).ready(function () {
        initialize();
    });

    function initialize() {
        // Create charts
    }

    this.updateData = function(data) {
        // Update the charts
    }

    this.showData = function () {
        // Show the tab data
        _performanceTabElement.html(_performanceTab.html());
        _healthTabElement.html(_healthTab.html());
    }
}