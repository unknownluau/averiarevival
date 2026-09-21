import { useEffect, useMemo, useState } from "react";
import { createUseStyles } from "react-jss";
import dayjs from "../../lib/dayjs";
import { acceptTrade, declineTrade, getTradeDetails } from "../../services/trades";
import AuthenticationStore from "../../stores/authentication";
import useButtonStyles from "../../styles/buttonStyles";
import Currency from "../Currency";
import { CurrencySize, CurrencyType } from "../../models/enums";
import ActionButton from "../actionButton";
import ItemImage from "../itemImage";
import NewModal from "../newModal";
import NewTradePage from "../newTradePage";
import PlayerHeadshot from "../playerHeadshot";
import TradeStore from "../myMoney/stores/tradeStore";
import FeedbackStore from "../../stores/feedback";
import { FeedbackType } from "../../models/feedback";

const tradeTypes = [
  { value: "inbound", label: "Inbound" },
  { value: "outbound", label: "Outbound" },
  { value: "completed", label: "Completed" },
  { value: "inactive", label: "Inactive" },
];

const useStyles = createUseStyles({
  page: {
    width: "100%",
    minHeight: "calc(100vh - 88px)",
    color: "var(--text-color-primary)",
    background: "transparent",
    marginTop: -12,
    padding: "22px 24px 64px",
    fontFamily: "Arial, Helvetica, sans-serif",
    "& *": {
      boxSizing: "border-box",
    },
  },
  shell: {
    width: "100%",
    maxWidth: 1188,
    margin: "0 auto",
    display: "grid",
    gridTemplateColumns: "396px minmax(0, 1fr)",
    gap: 48,
    alignItems: "start",
  },
  listHeader: {
    display: "grid",
    gridTemplateColumns: "1fr 198px",
    alignItems: "center",
    gap: 20,
  },
  title: {
    margin: 0,
    fontSize: 34,
    lineHeight: 1.1,
    fontWeight: 500,
  },
  select: {
    height: 39,
    width: "100%",
    border: "1px solid var(--text-color-secondary)",
    borderRadius: 0,
    background: "var(--white-color)",
    color: "var(--text-color-primary)",
    fontSize: 18,
    fontWeight: 400,
    padding: "0 36px 0 12px",
    outline: 0,
  },
  helpLink: {
    display: "inline-block",
    marginTop: 10,
    color: "var(--text-color-primary)",
    fontSize: 14,
    fontWeight: 400,
    textDecoration: "underline",
    "&:hover": {
      color: "var(--primary-color)",
    },
  },
  tradeList: {
    marginTop: 15,
    border: "1px solid var(--text-color-quinary)",
    background: "var(--white-color)",
    height: "min(774px, calc(100vh - 220px))",
    minHeight: 420,
    overflowY: "auto",
  },
  tradeRow: {
    position: "relative",
    width: "100%",
    minHeight: 83,
    display: "grid",
    gridTemplateColumns: "58px 1fr 66px",
    gap: 8,
    alignItems: "center",
    padding: "10px 10px 9px 14px",
    border: 0,
    borderBottom: "1px solid var(--background-color)",
    color: "var(--text-color-primary)",
    background: "transparent",
    textAlign: "left",
    cursor: "pointer",
    "&:hover": {
      background: "color-mix(in srgb, var(--text-color-primary) 8%, transparent)",
    },
  },
  tradeRowSelected: {
    background: "color-mix(in srgb, var(--text-color-primary) 14%, transparent)",
    "&:hover": {
      background: "color-mix(in srgb, var(--text-color-primary) 17%, transparent)",
    },
  },
  avatarFrame: {
    width: 48,
    height: 48,
    borderRadius: "50%",
    overflow: "hidden",
    background: "var(--white-color-hover)",
    border: "1px solid var(--text-color-quinary)",
    "& img": {
      width: "100%",
      height: "100%",
      objectFit: "cover",
    },
  },
  rowName: {
    display: "block",
    color: "var(--text-color-primary)",
    fontSize: 18,
    lineHeight: "22px",
    fontWeight: 500,
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  rowStatus: {
    color: "var(--text-color-tertiary)",
    fontSize: 13,
    lineHeight: "17px",
    fontWeight: 400,
  },
  rowDate: {
    color: "var(--text-color-tertiary)",
    fontSize: 15,
    lineHeight: "20px",
    alignSelf: "start",
    justifySelf: "end",
  },
  details: {
    minWidth: 0,
    paddingTop: 0,
  },
  detailTitle: {
    margin: "0 0 4px",
    fontSize: 34,
    lineHeight: 1.1,
    fontWeight: 500,
  },
  expires: {
    margin: "0 0 16px",
    color: "var(--text-color-tertiary)",
    fontSize: 19,
    lineHeight: "24px",
    fontWeight: 400,
  },
  section: {
    marginTop: 14,
  },
  sectionDivider: {
    borderTop: "1px solid var(--background-color)",
    paddingTop: 15,
    marginTop: 23,
  },
  sectionTitle: {
    margin: "0 0 8px",
    fontSize: 23,
    lineHeight: "29px",
    fontWeight: 500,
  },
  itemGrid: {
    display: "flex",
    flexWrap: "wrap",
    gap: "14px 16px",
    minHeight: 186,
  },
  itemCard: {
    width: 126,
    color: "var(--text-color-primary)",
  },
  itemThumb: {
    width: 126,
    height: 126,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    background: "var(--white-color-hover)",
    overflow: "hidden",
    "& img": {
      width: "100%",
      height: "100%",
      objectFit: "contain",
      paddingTop: "0 !important",
      margin: "0 !important",
      maxWidth: "100% !important",
    },
  },
  limitedBadge: {
    position: "absolute",
    left: 7,
    bottom: 6,
    height: 23,
    minWidth: 24,
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    padding: "0 7px",
    borderRadius: 12,
    background: "color-mix(in srgb, var(--text-color-primary) 52%, transparent)",
    color: "var(--white-color)",
    fontSize: 13,
    fontWeight: 500,
  },
  thumbWrap: {
    position: "relative",
    width: 126,
    height: 126,
  },
  itemName: {
    marginTop: 7,
    color: "var(--text-color-primary)",
    fontSize: 18,
    lineHeight: "22px",
    fontWeight: 500,
    minHeight: 44,
    overflow: "hidden",
  },
  itemValue: {
    marginTop: 2,
  },
  totalRow: {
    display: "grid",
    gridTemplateColumns: "1fr auto",
    gap: 16,
    alignItems: "center",
    maxWidth: 565,
    marginTop: 18,
    color: "var(--text-color-primary)",
    fontSize: 18,
    fontWeight: 500,
  },
  totalValue: {
    justifySelf: "end",
  },
  valueCurrency: {
    alignItems: "center",
    flexWrap: "nowrap",
    "& span": {
      float: "none !important",
    },
  },
  valueCurrencyIcon: {
    marginTop: "0 !important",
    marginRight: "5px !important",
  },
  valueCurrencyLabel: {
    color: "var(--robux-color) !important",
    fontSize: "18px !important",
    lineHeight: "23px !important",
    fontWeight: "500 !important",
    marginTop: "0 !important",
  },
  totalCurrencyLabel: {
    color: "var(--robux-color) !important",
    fontSize: "24px !important",
    lineHeight: "28px !important",
    fontWeight: "500 !important",
  },
  actions: {
    display: "flex",
    gap: 12,
    marginTop: 22,
  },
  actionButton: {
    minWidth: 82,
    minHeight: "36px",
    paddingLeft: "13px !important",
    paddingRight: "13px !important",
    fontSize: "18px !important",
  },
  emptyState: {
    padding: 22,
    color: "var(--text-color-tertiary)",
    fontSize: 17,
    fontWeight: 400,
  },
  feedback: {
    marginTop: 14,
    color: "var(--text-color-tertiary)",
    fontSize: 15,
    fontWeight: 400,
  },
  declineModalContainer: {
    "& h5": {
      fontSize: 24,
      fontWeight: 500,
      lineHeight: "29px",
      padding: "6px 0",
    },
  },
  declineModalBody: {
    textAlign: "center",
    padding: "6px 6px 0",
  },
  declineQuestion: {
    margin: "0 0 18px",
    color: "var(--text-color-tertiary)",
    fontSize: 18,
    lineHeight: "23px",
    fontWeight: 500,
  },
  declineButtons: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 14,
    marginBottom: 14,
  },
  declineModalBtn: {
    minWidth: 104,
    padding: "10px 14px !important",
    fontSize: "22px !important",
    lineHeight: "24px !important",
    fontWeight: "500 !important",
  },
  declineFooterCopy: {
    margin: 0,
    color: "var(--text-color-tertiary)",
    fontSize: 12,
    lineHeight: "17px",
    fontWeight: 600,
    "& a": {
      color: "var(--text-color-primary)",
      fontWeight: 500,
    },
  },
  acceptModalContainer: {
    "& h5": {
      fontSize: 22,
      fontWeight: 500,
      lineHeight: "27px",
      padding: "5px 0",
    },
  },
  acceptModalBody: {
    textAlign: "center",
    padding: "0 3px",
  },
  acceptQuestion: {
    margin: "0 0 14px",
    color: "var(--text-color-tertiary)",
    fontSize: 18,
    lineHeight: "23px",
    fontWeight: 400,
  },
  acceptButtons: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 11,
  },
  acceptModalBtn: {
    minWidth: 90,
    padding: "8px 13px !important",
    fontSize: "18px !important",
    lineHeight: "20px !important",
    fontWeight: "500 !important",
  },
  statusPill: {
    display: "inline-flex",
    alignItems: "center",
    minHeight: 34,
    marginTop: 20,
    padding: "0 13px",
    border: "1px solid var(--text-color-secondary)",
    borderRadius: 6,
    color: "var(--text-color-primary)",
    fontSize: 17,
    fontWeight: 400,
  },
  robuxLine: {
    width: "100%",
    color: "var(--text-color-tertiary)",
    fontSize: 16,
    fontWeight: 500,
    marginTop: 2,
  },
  "@media (max-width: 992px)": {
    page: {
      padding: "18px 14px 48px",
    },
    shell: {
      gridTemplateColumns: "1fr",
      gap: 26,
    },
    tradeList: {
      height: 420,
    },
    details: {
      paddingTop: 0,
    },
  },
  "@media (max-width: 560px)": {
    listHeader: {
      gridTemplateColumns: "1fr",
      gap: 10,
    },
    title: {
      fontSize: 30,
    },
    detailTitle: {
      fontSize: 30,
    },
    itemGrid: {
      gap: "12px 12px",
    },
    itemCard: {
      width: "calc(50% - 6px)",
    },
    thumbWrap: {
      width: "100%",
      height: "auto",
      aspectRatio: "1 / 1",
    },
    itemThumb: {
      width: "100%",
      height: "100%",
    },
  },
});

const formatNumber = (value) => Number(value || 0).toLocaleString();

const getItemValue = (item) => Number(item?.recentAveragePrice || item?.originalPrice || 0);

const getOfferValue = (offer) => {
  if (!offer) return 0;
  const itemValue = (offer.userAssets || []).reduce((sum, item) => sum + getItemValue(item), 0);
  return itemValue + Number(offer.robux || 0);
};

const RobuxValue = ({ value, total }) => {
  const s = useStyles();
  return <Currency
    currencyType={CurrencyType.Robux}
    price={value}
    size={CurrencySize["16x16"]}
    divClass={s.valueCurrency}
    iconClass={s.valueCurrencyIcon}
    labelClass={total ? s.totalCurrencyLabel : s.valueCurrencyLabel}
  />;
};

const ItemCard = ({ item }) => {
  const s = useStyles();
  const serial = item.serialNumber || item.serial;

  return <div className={s.itemCard}>
    <div className={s.thumbWrap}>
      <div className={s.itemThumb}>
        <ItemImage id={item.assetId} name={item.name} />
      </div>
      {serial ? <span className={s.limitedBadge}>#{serial}</span> : null}
    </div>
    <div className={s.itemName}>{item.name}</div>
    <div className={s.itemValue}><RobuxValue value={getItemValue(item)} /></div>
  </div>;
};

const OfferSection = ({ title, offer, divider }) => {
  const s = useStyles();
  const items = offer?.userAssets || [];

  return <section className={`${s.section} ${divider ? s.sectionDivider : ""}`}>
    <h2 className={s.sectionTitle}>{title}</h2>
    <div className={s.itemGrid}>
      {items.map((item) => <ItemCard key={item.id || item.userAssetId} item={item} />)}
      {Number(offer?.robux || 0) > 0 ? <div className={s.robuxLine}>Robux: {formatNumber(offer.robux)}</div> : null}
    </div>
    <div className={s.totalRow}>
      <span>Total Value:</span>
      <span className={s.totalValue}><RobuxValue value={getOfferValue(offer)} total /></span>
    </div>
  </section>;
};

const TradeRow = ({ trade, selected, onSelect }) => {
  const s = useStyles();
  const user = trade.user || {};

  return <button
    type="button"
    className={`${s.tradeRow} ${selected ? s.tradeRowSelected : ""}`}
    onClick={() => onSelect(trade)}
  >
    <span className={s.avatarFrame}>
      <PlayerHeadshot id={user.id} name={user.name} size={100} />
    </span>
    <span>
      <span className={s.rowName}>{user.displayName || user.name}</span>
      <span className={s.rowStatus}>{trade.status || "Open"}</span>
    </span>
    <span className={s.rowDate}>{dayjs(trade.created).format("M/D/YY")}</span>
  </button>;
};

const DeclineTradeModal = ({ isWorking, onCancel, onDecline }) => {
  const s = useStyles();
  const buttonStyles = useButtonStyles();

  return <NewModal
    title="Decline Request"
    containerWidth={477}
    containerClass={s.declineModalContainer}
    exitFunction={isWorking ? null : onCancel}
    headerBorder={true}
  >
    <div className={s.declineModalBody}>
      <p className={s.declineQuestion}>Are you sure you want to decline this trade?</p>
      <div className={s.declineButtons}>
        <ActionButton
          type="button"
          label={isWorking ? "Declining..." : "Decline"}
          buttonStyle={buttonStyles.newWarningButton}
          className={s.declineModalBtn}
          disabled={isWorking}
          onClick={onDecline}
        />
        <ActionButton
          type="button"
          label="Cancel"
          buttonStyle={buttonStyles.newCancelButton}
          className={s.declineModalBtn}
          disabled={isWorking}
          onClick={onCancel}
        />
      </div>
      <p className={s.declineFooterCopy}>
        Tired of lowball trades?<br />
        Update your Trade Quality setting on the <a href="/My/Account#!/privacy">Privacy page</a> within My Settings.
      </p>
    </div>
  </NewModal>;
};

const AcceptTradeModal = ({ isWorking, onCancel, onAccept }) => {
  const s = useStyles();
  const buttonStyles = useButtonStyles();

  return <NewModal
    title="Accept Request"
    containerWidth={394}
    containerClass={s.acceptModalContainer}
    exitFunction={isWorking ? null : onCancel}
    headerBorder={true}
  >
    <div className={s.acceptModalBody}>
      <p className={s.acceptQuestion}>Are you sure you want to accept this trade?</p>
      <div className={s.acceptButtons}>
        <ActionButton
          type="button"
          label={isWorking ? "Accepting..." : "Accept"}
          buttonStyle={buttonStyles.newBuyButton}
          className={s.acceptModalBtn}
          disabled={isWorking}
          onClick={onAccept}
        />
        <ActionButton
          type="button"
          label="Cancel"
          buttonStyle={buttonStyles.newCancelButton}
          className={s.acceptModalBtn}
          disabled={isWorking}
          onClick={onCancel}
        />
      </div>
    </div>
  </NewModal>;
};

const TradesPage = () => {
  const s = useStyles();
  const auth = AuthenticationStore.useContainer();
  const trades = TradeStore.useContainer();
  const buttonStyles = useButtonStyles();
  const rows = trades.trades?.data || [];
  const [details, setDetails] = useState(null);
  const [isWorking, setIsWorking] = useState(false);
  const [acceptModalOpen, setAcceptModalOpen] = useState(false);
  const [declineModalOpen, setDeclineModalOpen] = useState(false);
  const [message, setMessage] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [counterTradeDetails, setCounterTradeDetails] = useState(null);
  const feedback = FeedbackStore.useContainer();

  useEffect(() => {
    if (rows.length > 0 && !trades.selectedTrade) {
      trades.setSelectedTrade(rows[0]);
    }
  }, [rows, trades.selectedTrade]);

  useEffect(() => {
    if (!trades.selectedTrade?.id || auth.userId === null) {
      setDetails(null);
      return;
    }

    let cancelled = false;
    setDetails(null);
    setLoadError(null);

    getTradeDetails({ tradeId: trades.selectedTrade.id }).then((data) => {
      if (!cancelled) setDetails(data);
    }).catch((e) => {
      if (!cancelled) setLoadError(e.message || "Could not load trade.");
    });

    return () => {
      cancelled = true;
    };
  }, [trades.selectedTrade?.id, auth.userId]);

  const offers = useMemo(() => {
    const authenticated = details?.offers?.find((offer) => offer.user?.id === auth.userId);
    const other = details?.offers?.find((offer) => offer.user?.id !== auth.userId);
    return { authenticated, other };
  }, [details, auth.userId]);

  const canAccept = details && (details.status === "Open" || details.status === "Countered") && details.user?.id !== auth.userId;
  const canCancelOrDecline = details && (details.status === "Open" || details.status === "Countered");
  const partner = details?.user || trades.selectedTrade?.user;

  const closeCounterTrade = () => {
    setCounterTradeDetails(null);
    trades.refershTrades();
  };

  const submitAction = async (action) => {
    if (!details?.id) return false;
    setIsWorking(true);
    setMessage(null);
    try {
      await action({ tradeId: details.id });
      setMessage("Trade updated.");
      trades.setSelectedTrade(null);
      trades.refershTrades();
      return true;
    } catch (e) {
      setMessage(e.message || "Trade could not be updated.");
      return false;
    } finally {
      setIsWorking(false);
    }
  };

  if (counterTradeDetails) {
    return <NewTradePage
      counterTrade={counterTradeDetails}
      partnerUserId={counterTradeDetails.user?.id}
      onBack={closeCounterTrade}
    />;
  }

  return <div className={s.page}>
    {acceptModalOpen ? <AcceptTradeModal
      isWorking={isWorking}
      onCancel={() => setAcceptModalOpen(false)}
      onAccept={async () => {
        const ok = await submitAction(acceptTrade);
        if (ok) {
          setAcceptModalOpen(false);
          feedback.addFeedback("Successfully accepted. The trade is now being processed by our system.", FeedbackType.SUCCESS);
        }
      }}
    /> : null}
    {declineModalOpen ? <DeclineTradeModal
      isWorking={isWorking}
      onCancel={() => setDeclineModalOpen(false)}
      onDecline={async () => {
        const ok = await submitAction(declineTrade);
        if (ok) {
          setDeclineModalOpen(false)
          feedback.addFeedback("Successfully declined trade.", FeedbackType.SUCCESS)
        };
      }}
    /> : null}
    <div className={s.shell}>
      <aside>
        <div className={s.listHeader}>
          <h1 className={s.title}>Trades</h1>
          <select
            className={s.select}
            value={trades.tradeType}
            onChange={(e) => trades.setTradeType(e.target.value)}
            aria-label="Trade type"
          >
            {tradeTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
          </select>
        </div>
        <a className={s.helpLink} href="/help">How do I trade?</a>
        <div className={s.tradeList}>
          {rows.length === 0 && trades.trades ? <div className={s.emptyState}>No trades found.</div> : null}
          {!trades.trades ? <div className={s.emptyState}>Loading trades...</div> : null}
          {rows.map((trade) => <TradeRow
            key={trade.id}
            trade={trade}
            selected={trades.selectedTrade?.id === trade.id}
            onSelect={(value) => {
              setMessage(null);
              setAcceptModalOpen(false);
              setDeclineModalOpen(false);
              trades.setSelectedTrade(value);
            }}
          />)}
        </div>
      </aside>

      <main className={s.details}>
        {partner ? <h1 className={s.detailTitle}>Trade with {partner.displayName || partner.name}</h1> : <h1 className={s.detailTitle}>Trade Details</h1>}
        {details?.expiration ? <p className={s.expires}>Expires on {dayjs(details.expiration).format("M/D/YY")}</p> : null}
        {loadError ? <div className={s.emptyState}>{loadError}</div> : null}
        {!loadError && !details && trades.selectedTrade ? <div className={s.emptyState}>Loading trade...</div> : null}
        {!trades.selectedTrade && rows.length === 0 ? <div className={s.emptyState}>Select a trade type.</div> : null}
        {details ? <>
          <OfferSection title="Items you will give" offer={offers.authenticated} />
          <OfferSection title="Items you will receive" offer={offers.other} divider />
          {canAccept || canCancelOrDecline ? <div className={s.actions}>
            {canAccept ? <ActionButton
              type="button"
              label="Accept"
              className={s.actionButton}
              buttonStyle={buttonStyles.newContinueButton}
              disabled={isWorking}
              onClick={() => setAcceptModalOpen(true)}
            /> : null}
            {canAccept ? <ActionButton
              type="button"
              label="Counter"
              className={s.actionButton}
              buttonStyle={buttonStyles.newCancelButton}
              disabled={isWorking}
              onClick={() => setCounterTradeDetails(details)}
            /> : null}
            {canCancelOrDecline ? <ActionButton
              type="button"
              label={canAccept ? "Decline" : "Cancel"}
              className={s.actionButton}
              buttonStyle={buttonStyles.newCancelButton}
              disabled={isWorking}
              onClick={() => canAccept ? setDeclineModalOpen(true) : submitAction(declineTrade)}
            /> : null}
          </div> : null}
          {message ? <div className={s.feedback}>{message}</div> : null}
        </> : null}
      </main>
    </div>
  </div>;
};

export default TradesPage;
