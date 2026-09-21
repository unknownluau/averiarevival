<script lang="ts">
	import { onMount } from "svelte";
	import Chart from "chart.js";

	export let title: string;
	export let unit: string;
	export let series: { name: string; points: { Timestamp: string; Value: number }[] }[];
	let canvas: HTMLCanvasElement;
	let instance: Chart | undefined;
	const colors = ["#4dabf7", "#51cf66", "#ffd43b", "#ff6b6b", "#b197fc", "#22b8cf"];

	onMount(() => {
		const labels = series.length ? series[0].points.map(point => new Date(point.Timestamp).toLocaleTimeString()) : [];
		instance = new Chart(canvas.getContext("2d"), {
			type: "line",
			data: {
				labels,
				datasets: series.map((item, index) => ({
					label: item.name,
					data: item.points.map(point => point.Value),
					borderColor: colors[index % colors.length],
					backgroundColor: "transparent",
					borderWidth: 2,
					pointRadius: 0,
					lineTension: 0.15,
				})),
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,
				legend: { labels: { fontColor: "#adb5bd" } },
				scales: {
					xAxes: [{ ticks: { fontColor: "#adb5bd", maxTicksLimit: 8 }, gridLines: { color: "rgba(255,255,255,.06)" } }],
					yAxes: [{ ticks: { fontColor: "#adb5bd", beginAtZero: true }, gridLines: { color: "rgba(255,255,255,.06)" }, scaleLabel: { display: true, labelString: unit, fontColor: "#adb5bd" } }],
				},
				tooltips: { mode: "index", intersect: false },
			},
		});
		return () => instance && instance.destroy();
	});
</script>

<div class="card mod-card-dark telemetry-chart-card">
	<div class="card-body">
		<h5>{title}</h5>
		<div class="chart-container"><canvas bind:this={canvas}></canvas></div>
	</div>
</div>

<style>
	.telemetry-chart-card { height: 100%; }
	.chart-container { position: relative; height: 280px; }
</style>
