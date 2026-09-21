import { createUseStyles } from "react-jss";
import React, { useEffect, useMemo, useRef, useState } from "react";
import Highcharts from "highcharts";
import HighchartsReact from "highcharts-react-official";
import AssetDetailsStore from "../stores/AssetDetailsStore";
import { CurrencyType } from "../../../models/enums";
import Currency from "../../Currency";
import { getTheme, themeType } from "../../../services/theme";

const timelines = [
    { label: "30 Days", days: 30 },
    { label: "90 Days", days: 90 },
    { label: "180 Days", days: 180 },
];

const useStyles = createUseStyles({
    containerHeader: {
        margin: "3px 0 6px",
        "& h3": {
            fontSize: 20,
            fontWeight: 700,
            margin: 0,
        },
    },
    body: {
        display: "flex",
        flexDirection: "column",
    },
    topRow: {
        display: "flex",
        justifyContent: "space-between",
        alignItems: "flex-start",
        marginBottom: 8,
        flexWrap: "wrap",
        gap: 8,
    },
    legend: {
        display: "flex",
        alignItems: "center",
        gap: 16,
    },
    legendItem: {
        display: "flex",
        alignItems: "center",
        gap: 6,
        cursor: "pointer",
        userSelect: "none",
        fontSize: 14,
        fontWeight: 500,
        color: "var(--text-color-primary)",
    },
    legendItemOff: {
        color: "var(--text-color-tertiary)",
    },
    dash: {
        display: "inline-block",
        width: 18,
        height: 3,
        borderRadius: 2,
    },
    dashGreen: { background: "#02b757" },
    dashGrey: { background: "#b8b8b8" },
    dropdown: {
        position: "relative",
        width: 150,
        userSelect: "none",
    },
    dropdownButton: {
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        width: "100%",
        padding: "6px 12px",
        height: 32,
        border: "1px solid var(--text-color-quinary)",
        borderRadius: 4,
        background: "var(--white-color)",
        color: "var(--text-color-primary)",
        fontSize: 14,
        fontWeight: 500,
        cursor: "pointer",
        "&:hover": { borderColor: "var(--text-color-tertiary)" },
    },
    dropdownButtonOpen: {
        background: "var(--primary-color)",
        borderColor: "var(--primary-color)",
        color: "#fff",
        "&:hover": { borderColor: "var(--primary-color)" },
    },
    caret: {
        width: 0,
        height: 0,
        marginLeft: 8,
        borderLeft: "5px solid transparent",
        borderRight: "5px solid transparent",
        borderTop: "5px solid currentColor",
    },
    dropdownList: {
        position: "absolute",
        top: "calc(100% + 2px)",
        left: 0,
        right: 0,
        background: "var(--white-color)",
        border: "1px solid var(--text-color-quinary)",
        borderRadius: 4,
        boxShadow: "0 2px 6px rgba(0,0,0,0.25)",
        zIndex: 5,
        overflow: "hidden",
    },
    dropdownOption: {
        padding: "8px 12px",
        fontSize: 14,
        fontWeight: 500,
        color: "var(--text-color-primary)",
        cursor: "pointer",
        textAlign: "left",
        "&:hover": { background: "var(--white-color-hover)" },
    },
    divider: {
        height: 1,
        background: "var(--text-color-quinary)",
        margin: "8px 0 12px",
    },
    stats: {
        display: "flex",
        justifyContent: "space-around",
        alignItems: "flex-start",
        padding: "4px 0 12px",
    },
    stat: {
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: 8,
    },
    statLabel: {
        color: "var(--text-color-tertiary)",
        fontSize: 14,
        fontWeight: 500,
    },
    statValue: {
        color: "var(--text-color-primary)",
        fontSize: 16,
        fontWeight: 500,
    },
    spinnerWrap: {
        minHeight: 220,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
    },
    empty: {
        minHeight: 200,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        color: "var(--text-color-tertiary)",
        fontSize: 14,
    },
});

function getChartOptions(priceData, volumeData, showPrice, showVolume, isDark) {
    const axisText = isDark ? "#c3c3c3" : "#193a5e";
    const gridColor = isDark ? "rgba(255,255,255,0.08)" : "#ededed";
    const axisLine = isDark ? "rgba(255,255,255,0.15)" : "#e3e3e3";
    return {
        chart: {
            height: 200,
            spacing: [10, 0, 0, 0],
            backgroundColor: "transparent",
            style: {
                fontFamily: "HCo Gotham SSm,Helvetica Neue,Helvetica,Arial,Lucida Grande,sans-serif",
            },
        },
        credits: { enabled: false },
        title: { text: undefined },
        legend: { enabled: false },
        xAxis: {
            type: "datetime",
            lineColor: axisLine,
            tickColor: axisLine,
            tickLength: 4,
            dateTimeLabelFormats: {
                day: "%m/%d",
                week: "%m/%d",
                month: "%m/%d",
            },
            labels: {
                style: { color: axisText, fontSize: "11px", fontWeight: "500" },
            },
            crosshair: {
                width: 1,
                color: "rgba(2,183,87,0.25)",
            },
        },
        yAxis: [
            {
                title: { text: null },
                gridLineColor: gridColor,
                tickAmount: 3,
                top: "0%",
                height: "72%",
                offset: 0,
                lineWidth: 0,
                labels: {
                    style: { color: axisText, fontSize: "11px", fontWeight: "500" },
                    x: -4,
                    formatter: function () {
                        return this.value >= 1000 ? (this.value / 1000) + "K" : this.value;
                    },
                },
            },
            {
                title: { text: null },
                gridLineWidth: 0,
                top: "78%",
                height: "22%",
                offset: 0,
                lineWidth: 0,
                labels: { enabled: false },
            },
        ],
        tooltip: {
            useHTML: true,
            shared: true,
            split: false,
            followPointer: false,
            hideDelay: 0,
            snap: 1000,
            backgroundColor: "#4a4a4a",
            borderColor: "#4a4a4a",
            borderRadius: 4,
            borderWidth: 0,
            shadow: false,
            padding: 0,
            style: { color: "#fff" },
            formatter: function () {
                const pricePoint = this.points?.find(p => p.series.name === "Recent Average Price");
                const y = pricePoint ? pricePoint.y : this.y;
                return `<div style="padding:6px 10px;color:#fff;font-weight:500;font-size:13px;">${Math.round(y).toLocaleString()}</div>`;
            },
        },
        plotOptions: {
            series: {
                animation: { duration: 400 },
                stickyTracking: true,
                states: {
                    hover: { lineWidthPlus: 0, halo: { size: 0 } },
                    inactive: { opacity: 1 },
                },
                marker: {
                    enabled: true,
                    radius: 3,
                    lineWidth: 0,
                    symbol: "circle",
                    states: {
                        hover: { enabled: true, radius: 5, lineWidth: 2, lineColor: "#fff" },
                    },
                },
            },
            column: {
                borderWidth: 0,
                pointPadding: 0,
                groupPadding: 0,
                pointWidth: 1,
                enableMouseTracking: false,
            },
            line: { lineWidth: 2 },
        },
        series: [
            {
                type: "line",
                name: "Recent Average Price",
                data: priceData,
                color: "#02b757",
                visible: showPrice,
                yAxis: 0,
                zIndex: 2,
            },
            {
                type: "column",
                name: "Volume",
                data: volumeData,
                color: isDark ? "#888" : "#b8b8b8",
                visible: showVolume,
                yAxis: 1,
                zIndex: 1,
            },
        ],
    };
}

function ResalePriceChart({ isLabelHidden = false }) {
    const s = useStyles();
    const store = AssetDetailsStore.useContainer();
    const [timelineIdx, setTimelineIdx] = useState(2);
    const [dropdownOpen, setDropdownOpen] = useState(false);
    const [showPrice, setShowPrice] = useState(true);
    const [showVolume, setShowVolume] = useState(true);
    const [isDark, setIsDark] = useState(false);
    const dropdownRef = useRef(null);

    useEffect(() => {
        if (typeof window === "undefined") return;
        setIsDark(getTheme() === themeType.dark);
    }, []);

    useEffect(() => {
        if (!dropdownOpen) return;
        const onClick = e => {
            if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
                setDropdownOpen(false);
            }
        };
        document.addEventListener("mousedown", onClick);
        return () => document.removeEventListener("mousedown", onClick);
    }, [dropdownOpen]);

    const resale = store.resaleData;

    const { priceData, volumeData } = useMemo(() => {
        if (!resale?.priceDataPoints?.length) return { priceData: [], volumeData: [] };
        const cutoff = Date.now() - timelines[timelineIdx].days * 24 * 60 * 60 * 1000;
        const toPoint = d => [new Date(d.date).getTime(), d.value];
        const inRange = p => p[0] >= cutoff;
        return {
            priceData: resale.priceDataPoints.map(toPoint).filter(inRange).sort((a, b) => a[0] - b[0]),
            volumeData: resale.volumeDataPoints.map(toPoint).filter(inRange).sort((a, b) => a[0] - b[0]),
        };
    }, [resale, timelineIdx]);

    const hasData = priceData.length > 0 || volumeData.length > 0;

    const options = useMemo(
        () => getChartOptions(priceData, volumeData, showPrice, showVolume, isDark),
        [priceData, volumeData, showPrice, showVolume, isDark],
    );

    const originalCurrency = store.details?.priceTickets ? CurrencyType.Tickets : CurrencyType.Robux;
    const originalPrice = store.details?.priceTickets || store.details?.price || 0;
    const averagePrice = resale?.recentAveragePrice ?? 0;
    const quantitySold = resale?.sales ?? 0;

    const header = !isLabelHidden ? (
        <div className={`flex ${s.containerHeader}`}>
            <h3>Price Chart</h3>
        </div>
    ) : null;

    if (!resale) {
        return (
            <div>
                {header}
                <div className="section-content noShadow">
                    <div className={s.spinnerWrap}>
                        <span className="spinner" style={{ height: "100%", backgroundSize: "auto 36px" }}/>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div>
            {header}
            <div className={`section-content noShadow ${s.body}`}>
                <div className={s.topRow}>
                    <div className={s.legend}>
                        <div
                            className={`${s.legendItem} ${!showPrice ? s.legendItemOff : ""}`}
                            onClick={() => setShowPrice(v => !v)}
                        >
                            <span className={`${s.dash} ${showPrice ? s.dashGreen : s.dashGrey}`}/>
                            Recent Average Price
                        </div>
                        <div
                            className={`${s.legendItem} ${!showVolume ? s.legendItemOff : ""}`}
                            onClick={() => setShowVolume(v => !v)}
                        >
                            <span className={`${s.dash} ${s.dashGrey}`}/>
                            Volume
                        </div>
                    </div>
                    <div className={s.dropdown} ref={dropdownRef}>
                        <div
                            className={`${s.dropdownButton} ${dropdownOpen ? s.dropdownButtonOpen : ""}`}
                            onClick={() => setDropdownOpen(v => !v)}
                        >
                            <span>{timelines[timelineIdx].label}</span>
                            <span className={s.caret}/>
                        </div>
                        {dropdownOpen && (
                            <div className={s.dropdownList}>
                                {timelines.map((opt, i) => (
                                    <div
                                        key={opt.days}
                                        className={s.dropdownOption}
                                        onClick={() => {
                                            setTimelineIdx(i);
                                            setDropdownOpen(false);
                                        }}
                                    >
                                        {opt.label}
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </div>
                {hasData ? (
                    <HighchartsReact highcharts={Highcharts} options={options}/>
                ) : (
                    <div className={s.empty}>No sales recorded in this period.</div>
                )}
                <div className={s.divider}/>
                <div className={s.stats}>
                    <div className={s.stat}>
                        <span className={s.statLabel}>Quantity Sold</span>
                        <span className={s.statValue}>{quantitySold.toLocaleString()}</span>
                    </div>
                    <div className={s.stat}>
                        <span className={s.statLabel}>Original Price</span>
                        <Currency canBeFree price={originalPrice} currencyType={originalCurrency}/>
                    </div>
                    <div className={s.stat}>
                        <span className={s.statLabel}>Average Price</span>
                        <Currency price={averagePrice} currencyType={CurrencyType.Robux}/>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default ResalePriceChart;
