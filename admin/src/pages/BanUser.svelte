<script lang="ts">
	import { navigate } from 'svelte-routing';
	export let userId: string;

	import Main from '../components/templates/Main.svelte';
	import request from '../lib/request';
	import { is as isRank } from '../stores/rank';

	const quickFillReasons: { name: string; text: string }[] = [
		{
			name: 'TOS Violation',
			text: 'This account has been closed due to violating Averia terms of service.',
		},
		{
			name: 'Bad Username',
			text: 'Your username is inappropriate for Averia.',
		},
		{
			name: 'Bad Username (Privacy Issue)',
			text: 'Your username is not appropriate for Averia due to privacy concerns. ',
		},
		{
			name: 'Spam',
			text: 'Do not repeatedly post spam chat or content in Averia.',
		},
		{
			name: 'Inappropriate Behaviour',
			text: 'Your account has been deleted for creating, promoting, or participating in inappropriate behavior or content. This is a violation of our Terms of Use.',
		},
		{
			name: 'Hate Speech',
			text: 'This content is not appropriate. Hate speech is not permitted on Averia.',
		},
		{
			name: 'Real-Life Information',
			text: 'Do not ask for or give out personal, real-life, or private information on Averia.',
		},
		{
			name: 'Disputed Charges',
			text: 'Your account has been moderated because one or more of the charges on the account were reported as unauthorized or disputed by the billing account holder.',
		},
		{
			name: 'USDer',
			text: 'Your account has been moderated for buying, selling, or trading Robux or virtual Averia items outside of the Averia website.',
		},
		{
			name: 'Pois Lims',
			text: 'Your account has been moderated for facilitating account theft by receiving and/or moving stolen items.',
		},
		{
			name: 'Closed as Compromised',
			text: 'This account has been closed as a compromised account and will not be reopened.',
		},
		{
			name: 'Scamming',
			text: 'Scamming is a violation of the Terms of Service.',
		},
		{
			name: 'Account Theft',
			text: "Your account has been deleted for theft of an account and/or it's assets.",
		},
	];

	let disabled = false;
	let errorMessage: string | undefined;
	let expires: string|undefined;
	let internalReason: string|undefined;
	let isMachineBan = false;
	const genericTosReason = 'This account has been closed due to violating Averia terms of service.';
	const isOwner = isRank('owner');
</script>

<svelte:head>
	<title>Ban a User</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12 col-md-6">
			<h1>Ban User</h1>
			{#if errorMessage}
				<p class="red-text">{errorMessage}</p>
			{/if}
		</div>
	</div>
	<div class="row">
		<div class="col-12">
			<textarea {disabled} class="form-control" placeholder="Ban Reason" id="deletion-reason" />
			<textarea {disabled} class="form-control mt-2" placeholder="Internal Reason (only visible to staff)" bind:value={internalReason} />
			<div class="row mt-4">
				<div class="col-12 col-lg-3">
					<select class="form-control" bind:value={expires} disabled={disabled || isMachineBan}>
						<option value="permanent">Permanent</option>
						<option value="1,seconds">Warning</option>
						<option value="1,days">1 Day</option>
						<option value="3,days">3 Days</option>
						<option value="7,days">1 Week</option>
						<option value="14,days">2 Weeks</option>
						<option value="30,days">1 Month</option>
						<option value="365,days">1 Year</option>
					</select>
				</div>
				{#if isOwner}
					<div class="col-12 col-lg-4 d-flex align-items-center mt-3 mt-lg-0">
						<label class="mb-0">
							<input type="checkbox" bind:checked={isMachineBan} {disabled} />
							Silent machine ban
						</label>
					</div>
				{/if}
			</div>
			{#if isMachineBan}
				<p class="text-muted mt-2">The public reason is forced to the generic Terms of Service termination reason. Enforcement occurs after the client validates its machine.</p>
			{/if}

			<h3 class="mt-4">Quick Fill</h3>
			<div>
				<div class="btn-group">
					{#each quickFillReasons as reason}
						<button
							{disabled}
							class="btn-outline-primary btn"
							on:click={(e) => {
								e.preventDefault();
								document.getElementById('deletion-reason').innerText = reason.text;
							}}>{reason.name}</button
						>
					{/each}
				</div>
			</div>
			<button
				class="btn-success btn mt-4"
				{disabled}
				on:click={(e) => {
					// @ts-ignore
					let reason = isMachineBan ? genericTosReason : document.getElementById('deletion-reason').value;
					let expiresUtc = Date.now();
					let expiresStr = '';
					if (!isMachineBan && expires !== 'permanent') {
						let [val, period] = expires.split(',');
						let periodToMsec = period === 'seconds' ? 1000 : period === 'hours' ? (1000 * 60 * 60) : period === 'days' ? (86400 * 1000) : 0;
						expiresUtc += parseInt(val, 10) * periodToMsec;
						expiresStr = new Date(expiresUtc).toISOString();
					}
					if (!internalReason || internalReason.length < 3) {
						errorMessage = 'Internal reason is required.';
						return
					}
					disabled = true;
					request
						.post('/ban', {
							userId,
							reason,
							internalReason: internalReason || null,
							expires: expiresStr,
							isMachineBan,
						})
						.then((d) => {
							navigate('/admin/manage-user/' + userId);
						})
						.catch((e) => {
							errorMessage = (e && e.response && e.response.data) || 'Something went wrong. Please try again.';
						})
						.finally(() => {
							disabled = false;
						});
				}}>Submit</button
			>
		</div>
	</div>
</Main>
