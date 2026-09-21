import React, { useEffect, useRef, useState } from "react";
import { createUseStyles } from "react-jss";
import { redeemRewarbleVoucher } from "../../services/donations";
import { getTheme, themeType } from "../../services/theme";
import AuthenticationStore from "../../stores/authentication";

const tiers = [
    //{ amount: 5, name: "Saturn's Ring", img: '/img/DonatorItems/SaturnsRing.png', assetId: '764757', robux: 500 },
    { amount: 10, name: "Peasant's Crown of Nihilus", img: 'https://cdn.averia.lol/images/thumbnails/c53972802762289e105cf642060f7ecb751dd1eeff1a984194bf296e9bd42531.png', assetId: '957733', robux: 1100 },
    { amount: 15, name: "Glacial Demise", img: 'https://cdn.averia.lol/images/thumbnails/c40898c0f3cb805e24f78864c7e4a7ef88651c48760713ba5f8d6bedc2a41583.png', assetId: '957793', robux: 1750 },
    { amount: 25, name: 'Doomspire Warhelm of Doom', img: 'https://cdn.averia.lol/images/thumbnails/992a698e6f0aa56829c2c8cac1fa657e00eb7e4edaf387c655979349886e97e0.png', assetId: '661866', robux: 3100, popular: true },
    { amount: 50, name: 'Living Art: Combat on the Scorched Heights', img: 'https://cdn.averia.lol/images/thumbnails/99d4c0ad7942542dcc518da64ecc4a77c7ea0c40eeaf186a86c1f6a4369e641f.png', assetId: '957727', robux: 6000, bundle: true },
];

const cryptoMethods = [
    {
        name: 'Ethereum',
        ticker: 'ETH',
        address: '0xc7E913Ba2836653e571A7b63291c72C777eA1722',
    },
    {
        name: 'Bitcoin',
        ticker: 'BTC',
        address: 'bc1qaqqlh2x5a6getaqkfkrh945vzewe6wwg6ucqgp',
    },
    {
        name: 'Litecoin',
        ticker: 'LTC',
        address: 'Le7B2ncBUy2bm3KU3WyjGWua7S828Uc2Au',
    },
    {
        name: 'Solana',
        ticker: 'SOL',
        address: 'AQgEqtnNAK7G43FZLhZMyc8p5gKh1j5MMy4Pg59CTiy5',
    },
];

const rewarbleGiftCardSources = [
    {
        title: 'CoinGate',
        note: 'Card and Crypto Only (USA ONLY)',
        links: [
            {
                label: 'Rewarble Super Gift Card',
                href: 'https://coingate.com/gift-cards/rewarble-super',
            },
        ],
    },
    {
        title: 'Giftcards',
        note: 'PayPal & Card Accepted via G2A (USA ONLY)',
        links: [
            {
                label: 'G2A Rewarble Super Gift Card - $10 USD',
                href: 'https://www.g2a.com/rewarble-super-gift-card-10-usd-by-rewarble-key-united-states-i10000506957011',
            },
            {
                label: 'G2A Rewarble Super Gift Card - $15 USD',
                href: 'https://www.g2a.com/rewarble-super-gift-card-15-usd-by-rewarble-key-united-states-i10000506957019',
            },
            {
                label: 'G2A Rewarble Super Gift Card - $25 USD',
                href: 'https://www.g2a.com/rewarble-super-gift-card-25-usd-by-rewarble-key-united-states-i10000506957013',
            },
            {
                label: 'G2A Rewarble Super Gift Card - $50 USD',
                href: 'https://www.g2a.com/rewarble-super-gift-card-50-usd-by-rewarble-key-united-states-i10000506957015',
            },
        ],
    },
];

const useStyles = createUseStyles({
    wrapper: {
        paddingTop: '20px',
        paddingBottom: '40px',
    },
    hero: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019
            ? 'linear-gradient(135deg, #3d2c2c 0%, #252525 100%)'
            : 'linear-gradient(135deg, #fff7f2 0%, #ffffff 100%)',
        border: '1px solid rgba(138, 81, 73, 0.2)',
        borderRadius: '10px',
        boxShadow: '0 4px 16px rgba(25, 25, 25, 0.1)',
        padding: '26px 22px 22px',
        textAlign: 'center',
        marginBottom: '24px',
    },
    title: {
        color: 'var(--text-color-primary)',
        fontSize: '32px',
        fontWeight: 700,
        marginBottom: '8px',
    },
    subTitle: {
        fontSize: '15px',
        maxWidth: '640px',
        margin: '0 auto',
        lineHeight: 1.5,
    },
    heroHighlights: {
        display: 'flex',
        justifyContent: 'center',
        flexWrap: 'wrap',
        gap: '8px',
        marginTop: '18px',
    },
    heroHighlight: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#2a2a2a' : '#f6e9e5',
        color: 'var(--text-color-primary)',
        borderRadius: '20px',
        fontSize: '12px',
        fontWeight: 600,
        padding: '6px 11px',
    },
    grid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
        gap: '18px',
        '@media(max-width: 500px)': {
            gridTemplateColumns: '1fr',
        },
    },
    card: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '16px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        transition: 'transform 150ms ease, box-shadow 150ms ease',
        textDecoration: 'none',
        color: 'inherit',
        '&:hover': {
            transform: 'translateY(-3px)',
            boxShadow: '0 4px 10px rgba(25, 25, 25, 0.25)',
            textDecoration: 'none',
            color: 'inherit',
        },
    },
    selectedCard: {
        border: '2px solid var(--primary-color)',
        padding: '14px',
        boxShadow: '0 5px 16px rgba(138, 81, 73, 0.3)',
    },
    thumbWrap: {
        width: '100%',
        aspectRatio: '1 / 1',
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#2a2a2a' : 'var(--background-color)',
        borderRadius: '4px',
        overflow: 'hidden',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: '12px',
        position: 'relative',
    },
    thumb: {
        width: '100%',
        height: '100%',
        objectFit: 'contain',
    },
    badgeContainer: {
        position: 'absolute',
        top: 8,
        right: 8,
        display: 'flex',
        gap: 4,
        zIndex: 2,
    },
    badge: {
        display: 'inline-flex',
        alignItems: 'center',
        backgroundColor: '#F23515',
        color: '#fff',
        padding: '4px 6px',
        borderRadius: 3,
        fontWeight: 500,
        fontSize: 10,
        lineHeight: '1em',
        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.2)',
    },
    popularBadge: {
        backgroundColor: 'var(--primary-color)',
    },
    badgeClockIcon: {
        backgroundPosition: '-16px -576px!important',
        width: 16,
        height: 16,
        backgroundSize: '32px auto',
    },
    amount: {
        color: 'var(--primary-color)',
        fontSize: '26px',
        fontWeight: 700,
        margin: 0,
        marginBottom: '2px',
    },
    itemName: {
        color: 'var(--text-color-primary)',
        fontSize: '14px',
        fontWeight: 600,
        margin: '0 0 6px',
        minHeight: '20px',
        textAlign: 'center',
    },
    cadence: {
        fontSize: '13px',
        margin: 0,
        marginBottom: '10px',
    },
    robuxRow: {
        display: 'inline-flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        justifyContent: 'center',
        gap: '4px',
        fontSize: '14px',
        fontWeight: 600,
        margin: 0,
        marginBottom: '10px',
    },
    robuxIcon: {
        height: '12px',
        width: 'auto',
        verticalAlign: 'middle',
    },
    ctaRow: {
        width: '100%',
        marginTop: 'auto',
        textAlign: 'center',
        background: 'var(--primary-color)',
        color: 'white',
        fontWeight: 500,
        fontSize: '14px',
        padding: '7px 0',
        borderRadius: '4px',
        border: 'none',
        cursor: 'pointer',
        fontFamily: 'inherit',
        '&:hover': {
            opacity: 0.9,
        },
    },
    selectedCta: {
        background: 'var(--success-color)',
    },
    stripeBtn: {
        display: 'block',
        width: '100%',
        textAlign: 'center',
        background: 'var(--primary-color)',
        color: 'white',
        fontWeight: 500,
        fontSize: '14px',
        padding: '7px 0',
        borderRadius: '4px',
        marginTop: '10px',
        textDecoration: 'none',
        '&:hover': {
            opacity: 0.9,
            textDecoration: 'none',
            color: 'white',
        },
    },
    section: {
        marginTop: '44px',
    },
    sectionTitle: {
        color: 'var(--text-color-primary)',
        fontSize: '22px',
        fontWeight: 700,
        marginBottom: '12px',
        borderBottom: '1px solid var(--text-color-secondary)',
        paddingBottom: '6px',
    },
    perksBox: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '18px 22px',
        color: 'var(--text-color-primary)',
    },
    perkItem: {
        display: 'flex',
        alignItems: 'flex-start',
        marginBottom: '10px',
        '&:last-child': { marginBottom: 0 },
    },
    perkBadge: {
        display: 'inline-block',
        background: 'var(--primary-color)',
        color: 'white',
        fontSize: '12px',
        fontWeight: 600,
        padding: '3px 10px',
        borderRadius: '4px',
        marginRight: '10px',
        whiteSpace: 'nowrap',
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
    },
    perkText: {
        margin: 0,
        lineHeight: 1.5,
        fontSize: '14px',
    },
    paymentGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: '14px',
        '@media(max-width: 767px)': {
            gridTemplateColumns: '1fr',
        },
    },
    paymentCard: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '16px',
        display: 'flex',
        flexDirection: 'column',
    },
    paymentName: {
        fontSize: '16px',
        fontWeight: 500,
        color: 'var(--text-color-primary)',
        margin: 0,
        marginBottom: '4px',
    },
    paymentLink: {
        color: 'var(--primary-color)',
        fontSize: '15px',
        fontWeight: 500,
        wordBreak: 'break-all',
        '&:hover': {
            color: 'var(--primary-color-hover)',
            textDecoration: 'underline',
        },
    },
    paymentNote: {
        fontSize: '12px',
        marginTop: '6px',
        marginBottom: 0,
        fontStyle: 'italic',
        lineHeight: 1.4,
    },
    rewarbleGrid: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1fr) minmax(280px, 390px)',
        gap: '14px',
        '@media(max-width: 900px)': {
            gridTemplateColumns: '1fr',
        },
    },
    tutorialCard: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '16px',
        color: 'var(--text-color-primary)',
    },
    tutorialList: {
        margin: 0,
        paddingLeft: '20px',
        fontSize: '14px',
        lineHeight: 1.55,
        '& li': {
            marginBottom: '7px',
        },
        '& li:last-child': {
            marginBottom: 0,
        },
    },
    tutorialLinks: {
        display: 'flex',
        flexDirection: 'column',
        gap: '10px',
        marginTop: '14px',
    },
    vendorSource: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    vendorTitle: {
        color: 'var(--text-color-primary)',
        fontSize: '14px',
        fontWeight: 700,
        margin: 0,
    },
    vendorNote: {
        color: 'var(--text-color-secondary)',
        fontSize: '12px',
        fontWeight: 600,
        margin: 0,
    },
    vendorLinkList: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '4px',
    },
    tutorialLink: {
        color: 'var(--primary-color)',
        fontSize: '14px',
        fontWeight: 600,
        textDecoration: 'none',
        '&:hover': {
            color: 'var(--primary-color-hover)',
            textDecoration: 'underline',
        },
    },
    redeemCard: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '16px',
        color: 'var(--text-color-primary)',
    },
    redeemLabel: {
        display: 'block',
        color: 'var(--text-color-primary)',
        fontSize: '14px',
        fontWeight: 700,
        marginBottom: '7px',
    },
    voucherInput: {
        width: '100%',
        height: '40px',
        border: '1px solid var(--text-color-secondary)',
        borderRadius: '4px',
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#2a2a2a' : '#fff',
        color: 'var(--text-color-primary)',
        fontSize: '15px',
        fontWeight: 600,
        letterSpacing: '0.8px',
        padding: '0 11px',
        textTransform: 'uppercase',
        outline: 0,
        '&:focus': {
            borderColor: 'var(--primary-color)',
        },
    },
    redeemHint: {
        color: 'var(--text-color-secondary)',
        fontSize: '12px',
        lineHeight: 1.4,
        margin: '8px 0 0',
    },
    redeemButton: {
        width: '100%',
        minHeight: '38px',
        marginTop: '12px',
        border: 0,
        borderRadius: '4px',
        background: 'var(--success-color)',
        color: '#fff',
        fontSize: '15px',
        fontWeight: 700,
        cursor: 'pointer',
        '&:hover': {
            background: 'var(--success-color-hover)',
        },
        '&:disabled': {
            opacity: 0.5,
            cursor: 'not-allowed',
        },
    },
    redeemResult: {
        borderRadius: '4px',
        fontSize: '13px',
        lineHeight: 1.45,
        marginTop: '12px',
        padding: '10px 12px',
    },
    redeemSuccess: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#153923' : '#e5f7eb',
        color: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#a7e6bd' : '#176333',
        border: '1px solid rgba(31, 142, 72, 0.35)',
    },
    redeemWarning: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#3e3216' : '#fff4d8',
        color: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#f1d28a' : '#7a4b00',
        border: '1px solid rgba(180, 127, 0, 0.35)',
    },
    redeemError: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#432020' : '#fae5e5',
        color: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#ffb6b6' : '#8a1f11',
        border: '1px solid rgba(204, 0, 0, 0.35)',
    },
    resultTitle: {
        fontWeight: 700,
        margin: '0 0 3px',
    },
    resultText: {
        margin: 0,
    },
    cryptoGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: '14px',
        '@media(max-width: 767px)': {
            gridTemplateColumns: '1fr',
        },
    },
    cryptoCard: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '16px',
        display: 'flex',
        flexDirection: 'column',
    },
    cryptoName: {
        fontSize: '16px',
        fontWeight: 500,
        color: 'var(--text-color-primary)',
        margin: 0,
        marginBottom: '8px',
    },
    cryptoTicker: {
        fontWeight: 400,
        fontSize: '13px',
        marginLeft: '6px',
    },
    cryptoAddress: {
        width: '100%',
        fontFamily: 'monospace',
        fontSize: '13px',
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#2a2a2a' : '#f4f4f4',
        color: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#e0e0e0' : '#222',
        borderRadius: '4px',
        padding: '8px 12px',
        wordBreak: 'break-all',
        cursor: 'pointer',
        border: '1px solid transparent',
        transition: 'border-color 150ms ease, background 150ms ease',
        userSelect: 'none',
        textAlign: 'left',
        '&:hover': {
            borderColor: 'var(--primary-color)',
        },
        '&:focus-visible': {
            borderColor: 'var(--primary-color)',
            outline: '2px solid var(--primary-color)',
            outlineOffset: '2px',
        },
    },
    cryptoAddressCopied: {
        width: '100%',
        fontFamily: 'monospace',
        fontSize: '13px',
        background: 'var(--primary-color)',
        color: 'white',
        borderRadius: '4px',
        padding: '8px 12px',
        wordBreak: 'break-all',
        cursor: 'pointer',
        border: '1px solid transparent',
        transition: 'border-color 150ms ease, background 150ms ease',
        userSelect: 'none',
        textAlign: 'center',
        fontStyle: 'italic',
    },
    cryptoAddressError: {
        width: '100%',
        fontFamily: 'monospace',
        fontSize: '13px',
        background: '#FAE5E5',
        color: '#8a1f11',
        borderRadius: '4px',
        padding: '8px 12px',
        cursor: 'pointer',
        border: '1px solid #C00',
        textAlign: 'center',
    },
    disclaimer: {
        textAlign: 'center',
        marginTop: '28px',
        color: '#F23515',
        fontSize: '16px',
        fontWeight: 700,
    },
    countdownBanner: {
        background: '#F23515',
        borderRadius: '8px',
        padding: '18px 22px',
        marginBottom: '24px',
        color: 'white',
        boxShadow: '0 4px 14px rgba(242, 53, 21, 0.35)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
    },
    countdownNote: {
        fontSize: '12px',
        margin: '10px 0 0',
        opacity: 0.95,
    },
    countdownLabel: {
        fontSize: '13px',
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: '1.5px',
        margin: 0,
        marginBottom: '4px',
        opacity: 0.95,
    },
    countdownTitle: {
        fontSize: '20px',
        fontWeight: 700,
        margin: 0,
        marginBottom: '12px',
    },
    countdownGrid: {
        display: 'flex',
        gap: '10px',
        flexWrap: 'wrap',
        justifyContent: 'center',
    },
    countdownUnit: {
        background: 'rgba(0, 0, 0, 0.25)',
        borderRadius: '6px',
        padding: '8px 14px',
        minWidth: '68px',
    },
    countdownValue: {
        fontSize: '26px',
        fontWeight: 700,
        margin: 0,
        lineHeight: 1,
        fontVariantNumeric: 'tabular-nums',
    },
    countdownUnitLabel: {
        fontSize: '11px',
        textTransform: 'uppercase',
        letterSpacing: '1px',
        margin: 0,
        marginTop: '4px',
        opacity: 0.9,
    },
    showcaseBox: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '18px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '12px',
    },
    showcaseVideo: {
        width: '100%',
        maxWidth: '520px',
        borderRadius: '6px',
        background: '#000',
        display: 'block',
    },
    bundleTag: {
        display: 'inline-flex',
        background: 'var(--primary-color)',
        color: 'white',
        fontSize: '11px',
        fontWeight: 700,
        padding: '3px 8px',
        borderRadius: '4px',
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
        marginLeft: '3px',
    },
    selectionBox: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        border: '2px solid var(--primary-color)',
        borderRadius: '6px',
        boxShadow: '0 2px 7px rgba(25, 25, 25, 0.14)',
        marginBottom: '14px',
        padding: '14px 16px',
    },
    selectionTitle: {
        color: 'var(--text-color-primary)',
        fontSize: '16px',
        fontWeight: 700,
        margin: '0 0 4px',
    },
    selectionText: {
        fontSize: '13px',
        margin: 0,
    },
    displayNameNotice: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#2a2a2a' : '#fff7f2',
        border: '1px solid rgba(138, 81, 73, 0.4)',
        borderRadius: '4px',
        color: 'var(--text-color-primary)',
        fontSize: '13px',
        fontWeight: 600,
        lineHeight: 1.5,
        margin: '12px 0 0',
        padding: '10px 12px',
    },
    stepsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: '14px',
        '@media(max-width: 767px)': {
            gridTemplateColumns: '1fr',
        },
    },
    stepCard: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : 'var(--white-color)',
        borderRadius: '6px',
        boxShadow: '0 1px 3px rgba(25, 25, 25, 0.15)',
        padding: '16px',
    },
    stepNumber: {
        alignItems: 'center',
        background: 'var(--primary-color)',
        borderRadius: '50%',
        color: 'white',
        display: 'inline-flex',
        fontSize: '13px',
        fontWeight: 700,
        height: '26px',
        justifyContent: 'center',
        marginBottom: '8px',
        width: '26px',
    },
    stepTitle: {
        color: 'var(--text-color-primary)',
        fontSize: '15px',
        fontWeight: 700,
        margin: '0 0 4px',
    },
    stepText: {
        fontSize: '13px',
        lineHeight: 1.45,
        margin: 0,
    },
    claimBox: {
        background: p => p.theme === themeType.dark || p.theme === themeType.obc2019 ? '#393939' : '#fff7f2',
        border: '1px solid rgba(138, 81, 73, 0.3)',
        borderRadius: '6px',
        marginTop: '28px',
        padding: '16px 18px',
        textAlign: 'center',
    },
    claimTitle: {
        color: 'var(--text-color-primary)',
        fontSize: '17px',
        fontWeight: 700,
        margin: '0 0 6px',
    },
    claimText: {
        fontSize: '13px',
        lineHeight: 1.5,
        margin: '3px 0',
    },
});

const TARGET_DATE = new Date('2026-09-01T00:00:00');

const getTimeUntilTarget = () => {
    let diff = Math.max(0, TARGET_DATE.getTime() - Date.now());
    const days = Math.floor(diff / 86400000); diff -= days * 86400000;
    const hours = Math.floor(diff / 3600000); diff -= hours * 3600000;
    const minutes = Math.floor(diff / 60000); diff -= minutes * 60000;
    const seconds = Math.floor(diff / 1000);
    return { days, hours, minutes, seconds };
};

const copyText = async text => {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(text);
        return;
    }

    const textArea = document.createElement('textarea');
    textArea.value = text;
    textArea.style.position = 'fixed';
    textArea.style.opacity = '0';
    document.body.appendChild(textArea);
    textArea.select();
    const copied = document.execCommand('copy');
    document.body.removeChild(textArea);
    if (!copied) throw new Error('Copy command failed');
};

const CryptoAddress = ({ address, styles }) => {
    const [copyStatus, setCopyStatus] = useState('idle');
    const resetTimeout = useRef(null);

    useEffect(() => () => clearTimeout(resetTimeout.current), []);

    const handleClick = async () => {
        try {
            await copyText(address);
            setCopyStatus('copied');
        } catch (e) {
            setCopyStatus('error');
        }

        clearTimeout(resetTimeout.current);
        resetTimeout.current = setTimeout(() => setCopyStatus('idle'), 1600);
    };

    return (
        <button
            type="button"
            className={
                copyStatus === 'copied'
                    ? styles.cryptoAddressCopied
                    : copyStatus === 'error'
                        ? styles.cryptoAddressError
                        : styles.cryptoAddress
            }
            onClick={handleClick}
            title="Copy address"
            aria-label={`Copy ${address}`}
        >
            {copyStatus === 'copied'
                ? 'Copied to clipboard!'
                : copyStatus === 'error'
                    ? `Copy failed: ${address}`
                    : address}
        </button>
    );
};

const getRedeemErrorMessage = error => {
    const data = error?.response?.data;
    if (data?.message) return data.message;
    if (data?.errors?.[0]?.message) return data.errors[0].message;
    return error?.message || 'Voucher could not be redeemed.';
};

const getRedeemResultTitle = result => {
    if (!result) return '';
    if (result.status === 'granted') return 'Rewards granted';
    if (result.status === 'duplicate') return 'Voucher already processed';
    return 'Manual review needed';
};

const getRedeemResultBody = result => {
    if (!result) return '';
    if (result.status === 'granted' && result.reward) {
        const assets = result.reward.assetIds?.length || 0;
        return `${result.message} You received ${result.reward.robux.toLocaleString()} Robux${assets ? ` and ${assets} item${assets === 1 ? '' : 's'}` : ''}.`;
    }
    return result.message || 'Voucher redeemed, but rewards need manual review. Please contact support at https://support.averia.one/submit-request';
};

const Donate = () => {
    const s = useStyles({ theme: getTheme() });
    const auth = AuthenticationStore.useContainer();
    const [countdown, setCountdown] = useState(getTimeUntilTarget);
    const [selectedTier, setSelectedTier] = useState(tiers.find(tier => tier.popular));
    const [voucherCode, setVoucherCode] = useState('');
    const [redeeming, setRedeeming] = useState(false);
    const [redeemResult, setRedeemResult] = useState(null);
    const [redeemError, setRedeemError] = useState(null);

    useEffect(() => {
        const id = setInterval(() => setCountdown(getTimeUntilTarget()), 1000);
        return () => clearInterval(id);
    }, []);

    const selectTier = tier => {
        setSelectedTier(tier);
        document.getElementById('payment-methods').scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    const normalizedVoucherCode = voucherCode.trim().toUpperCase();
    const canRedeem = auth.isAuthenticated && normalizedVoucherCode.length >= 16 && !redeeming;

    const handleVoucherChange = event => {
        setVoucherCode(event.currentTarget.value.toUpperCase());
        setRedeemResult(null);
        setRedeemError(null);
    };

    const handleRedeem = async event => {
        event.preventDefault();
        if (!canRedeem) return;

        setRedeeming(true);
        setRedeemResult(null);
        setRedeemError(null);
        try {
            const result = await redeemRewarbleVoucher({ code: normalizedVoucherCode });
            setRedeemResult(result);
            setVoucherCode('');
        } catch (error) {
            setRedeemError(getRedeemErrorMessage(error));
        } finally {
            setRedeeming(false);
        }
    };

    return <div className={`container ${s.wrapper}`}>
        <div className={s.hero}>
            <h1 className={s.title}>Support Averia</h1>
            <p className={s.subTitle}>
                Averia is a privately owned, community-based project, not a registered non-profit or charity.
                Contributions help support the servers, development work, future expenses, and reserves that keep Averia running.
            </p>
            <p className={s.subTitle} style={{ marginTop: '10px' }}>
                While profit is not Averia's main purpose, it may receive more revenue than its operating costs.
                Pick a tier below to contribute and receive a limited in-game item as our thank-you.
            </p>
            <div className={s.heroHighlights}>
                <span className={s.heroHighlight}>Limited on-site rewards</span>
                <span className={s.heroHighlight}>Permanent Discord role</span>
                <span className={s.heroHighlight}>Community funding</span>
            </div>
        </div>

        <div className={s.countdownBanner}>
            <p className={s.countdownLabel}>Leaving Soon</p>
            <p className={s.countdownTitle}>These donation items disappear on August 1, 2026</p>
            <div className={s.countdownGrid}>
                <div className={s.countdownUnit}>
                    <p className={s.countdownValue}>{String(countdown.days).padStart(2, '0')}</p>
                    <p className={s.countdownUnitLabel}>Days</p>
                </div>
                <div className={s.countdownUnit}>
                    <p className={s.countdownValue}>{String(countdown.hours).padStart(2, '0')}</p>
                    <p className={s.countdownUnitLabel}>Hours</p>
                </div>
                <div className={s.countdownUnit}>
                    <p className={s.countdownValue}>{String(countdown.minutes).padStart(2, '0')}</p>
                    <p className={s.countdownUnitLabel}>Minutes</p>
                </div>
                <div className={s.countdownUnit}>
                    <p className={s.countdownValue}>{String(countdown.seconds).padStart(2, '0')}</p>
                    <p className={s.countdownUnitLabel}>Seconds</p>
                </div>
            </div>
            <p className={s.countdownNote}>Claim your reward before this limited collection retires.</p>
        </div>

        <div className={s.grid}>
            {tiers.map(tier => {
                const itemUrl = tier.assetId ? `https://www.averia.lol/catalog/${tier.assetId}/Donate` : null;
                const thumbInner = (
                    <>
                        <div className={s.badgeContainer}>
                            {tier.popular && <span className={`${s.badge} ${s.popularBadge}`}>Popular</span>}
                            <span className={s.badge}>New</span>
                            <span className={s.badge}>
                                <span className={`${s.badgeClockIcon} icon-clock`}/>
                            </span>
                        </div>
                        <img src={tier.img} alt={`$${tier.amount} donation item`} className={s.thumb}/>
                    </>
                );
                const isSelected = selectedTier.amount === tier.amount;
                return <div key={tier.amount} className={`${s.card} ${isSelected ? s.selectedCard : ''}`}>
                    {itemUrl ? (
                        <a
                            href={itemUrl}
                            target='_blank'
                            rel='noopener noreferrer'
                            className={s.thumbWrap}
                            style={{ display: 'flex' }}
                        >
                            {thumbInner}
                        </a>
                    ) : (
                        <div className={s.thumbWrap}>{thumbInner}</div>
                    )}
                    <p className={s.itemName}>{tier.name}</p>
                    <p className={s.amount}>${tier.amount}</p>
                    <p className={s.robuxRow}>
                        +{tier.robux.toLocaleString()}
                        <img src='/img/img-robux.png' alt='Robux' className={s.robuxIcon} />
                        {tier.bundle && <span className={s.bundleTag}>+ all items</span>}
                    </p>
                    <button
                        type="button"
                        className={`${s.ctaRow} ${isSelected ? s.selectedCta : ''}`}
                        onClick={() => selectTier(tier)}
                        aria-pressed={isSelected}
                    >{isSelected ? 'Selected' : `Choose $${tier.amount}`}</button>
                </div>;
            })}
        </div>

        <div className={s.section}>
            <h2 className={s.sectionTitle}>How It Works</h2>
            <div className={s.stepsGrid}>
                <div className={s.stepCard}>
                    <span className={s.stepNumber}>1</span>
                    <p className={s.stepTitle}>Choose a tier</p>
                    <p className={s.stepText}>Pick the reward you want. The $50 tier includes the full collection.</p>
                </div>
                <div className={s.stepCard}>
                    <span className={s.stepNumber}>2</span>
                    <p className={s.stepTitle}>Buy a voucher</p>
                    <p className={s.stepText}>Purchase a Rewarble voucher for the matching USD amount on Rewarble.</p>
                </div>
                <div className={s.stepCard}>
                    <span className={s.stepNumber}>3</span>
                    <p className={s.stepTitle}>Claim your rewards</p>
                    <p className={s.stepText}>Log in, paste your voucher code below, and rewards are granted to your Averia account automatically.</p>
                </div>
            </div>
        </div>

        <div className={s.section}>
            <h2 className={s.sectionTitle}>Showcase</h2>
            <div className={s.showcaseBox}>
                <video
                    className={s.showcaseVideo}
                    src='/img/DonatorItems/WIORIOKGB80T.mp4'
                    autoPlay
                    loop
                    muted
                    playsInline
                />
            </div>
        </div>

        <div className={s.section}>
            <h2 className={s.sectionTitle}>Perks</h2>
            <div className={s.perksBox}>
                <div className={s.perkItem}>
                    <span className={s.perkBadge}>Item</span>
                    <p className={s.perkText}>Rewarble voucher redemption grants the matching on-site item and Robux reward for your tier.</p>
                </div>
                <div className={s.perkItem}>
                    <span className={s.perkBadge}>Discord</span>
                    <p className={s.perkText}>
                        Get the <strong>Donator</strong> role in the Averia Discord server permanently.
                    </p>
                </div>
            </div>
        </div>

        <div className={s.section} id="payment-methods">
            <h2 className={s.sectionTitle}>Redeem a Rewarble Voucher</h2>
            <div className={s.selectionBox}>
                <p className={s.selectionTitle}>Your selected tier: ${selectedTier.amount} - {selectedTier.name}</p>
                <p className={s.selectionText}>
                    Your thank-you reward includes {selectedTier.bundle ? 'the complete item collection' : 'the matching limited item'},
                    {' '}{selectedTier.robux.toLocaleString()} Robux, and the permanent Discord Donator role.
                </p>
                <p className={s.displayNameNotice}>
                    Buy a <strong>${selectedTier.amount} USD</strong> Rewarble voucher, then redeem it here while logged in.
                    Rewards are granted to the Averia account currently signed in on this page.
                </p>
            </div>
            <div className={s.rewarbleGrid}>
                <div className={s.tutorialCard}>
                    <ol className={s.tutorialList}>
                        <li>Choose the Averia reward tier you want.</li>
                        <li>Use one of the links below to buy a Rewarble gift card for the matching USD amount.</li>
                        <li>Return to this page while logged into the Averia account that should receive the rewards.</li>
                        <li>Paste the voucher code into the redeem box and submit it once.</li>
                        <li>If anything goes wrong, contact support at https://support.averia.one/submit-request</li>
                    </ol>
                    <div className={s.tutorialLinks}>
                        {rewarbleGiftCardSources.map(source => (
                            <div className={s.vendorSource} key={source.title}>
                                <p className={s.vendorTitle}>{source.title}</p>
                                <p className={s.vendorNote}>{source.note}</p>
                                <div className={s.vendorLinkList}>
                                    {source.links.map(link => (
                                        <a className={s.tutorialLink} href={link.href} target="_blank" rel="noopener noreferrer" key={link.href}>{link.label}</a>
                                    ))}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
                <form className={s.redeemCard} onSubmit={handleRedeem}>
                    <label className={s.redeemLabel} htmlFor="rewarble-voucher-code">Rewarble voucher code</label>
                    <input
                        id="rewarble-voucher-code"
                        className={s.voucherInput}
                        value={voucherCode}
                        onChange={handleVoucherChange}
                        placeholder="PASTE VOUCHER CODE"
                        autoComplete="off"
                    />
                    <p className={s.redeemHint}>
                        Voucher codes must be at least 16 characters. Codes are automatically uppercased before redeeming.
                    </p>
                    {!auth.isAuthenticated && !auth.isPending ? <p className={s.redeemHint}>Log in before redeeming so rewards can be granted to your account.</p> : null}
                    <button
                        type="submit"
                        className={s.redeemButton}
                        disabled={!canRedeem}
                    >
                        {redeeming ? 'Redeeming...' : 'Redeem Voucher'}
                    </button>
                    {redeemResult ? <div className={`${s.redeemResult} ${redeemResult.status === 'granted' ? s.redeemSuccess : redeemResult.status === 'duplicate' ? s.redeemWarning : s.redeemWarning}`}>
                        <p className={s.resultTitle}>{getRedeemResultTitle(redeemResult)}</p>
                        <p className={s.resultText}>{getRedeemResultBody(redeemResult)}</p>
                    </div> : null}
                    {redeemError ? <div className={`${s.redeemResult} ${s.redeemError}`}>
                        <p className={s.resultTitle}>Voucher not redeemed</p>
                        <p className={s.resultText}>{redeemError}</p>
                    </div> : null}
                </form>
            </div>
        </div>

        <div className={s.section}>
            <h2 className={s.sectionTitle}>Cryptocurrency</h2>
            <div className={s.cryptoGrid}>
                {cryptoMethods.map(coin => (
                    <div key={coin.ticker} className={s.cryptoCard}>
                        <p className={s.cryptoName}>
                            {coin.name}
                            <span className={s.cryptoTicker}>{coin.ticker}</span>
                        </p>
                        <CryptoAddress address={coin.address} styles={s} />
                    </div>
                ))}
            </div>
        </div>

        <p className={s.disclaimer}>Donations are final and non-refundable.</p>
        <div className={s.claimBox}>
            <p className={s.claimTitle}>Need help?</p>
            <p className={s.claimText}>If anything goes wrong, go to https://support.averia.one/submit-request for customer support.</p>
            <p className={s.claimText}>Rewarble vouchers grant on-site items and Robux automatically when redeemed by a logged-in Averia account.</p>
            <p className={s.claimText}>Claims are usually processed sooner, but please allow up to 24 hours.</p>
        </div>
    </div>;
};

export default Donate;
