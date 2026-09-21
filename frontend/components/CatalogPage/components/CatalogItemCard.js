import {createUseStyles} from "react-jss";
import Link from "../../link";
import { ThumbnailFromState } from "../../AvatarEditorPage/components/avatarCardList";
import CreatorLink from "../../creatorLink";
import { abbreviateNumber } from "../../../lib/numberUtils";
import {IsNullOrEmpty, IsValidNum} from "../../../lib/utils";
import React, {useEffect, useState} from "react";
import {IsISOWithinDays} from "../../AssetDetailsPage";
import {getTheme, themeType} from "../../../services/theme";

const useStyles = createUseStyles({
    cardWrapper: {
        borderRadius: 3,
        //aspectRatio: "4 / 5",
        width: "calc(16.66666667% - 8px)",
        display: "flex",
        flexDirection: "column",
        "@media(max-width: 991px)": {
            width: "calc(20% - 8px)",
        },
        "@media(max-width: 767px)": {
            width: "calc(25% - 8px)",
        },
        "@media(max-width: 616px)": {
            width: "calc(33% - 8px)",
        },
        "@media(max-width: 373px)": {
            width: "calc(50% - 8px)",
        },
    },
    cardContainer: {
        width: "100%",
        height: "100%",
        flexDirection: "column",
        display: "flex",
        backgroundColor: "var(--white-color)",
        position: "relative",
        boxShadow: p => p.theme === themeType.dark ? "0 1px 4px 0 rgba(230,230,230,0.3)" : "0 1px 4px 0 rgba(25,25,25,0.3)",
        borderRadius: 3,
        maxWidth: 150,
        transition: "box-shadow 200ms ease",
        "-webkit-transition": "box-shadow 200ms ease",
        "&:hover": {
            boxShadow: p => p.theme === themeType.dark ? "0 1px 6px 0 rgba(230,230,230,0.75)" : "0 1px 6px 0 rgba(25,25,25,0.75)",
        }
    },
    cardImage: {
        cursor: "pointer",
        width: "100%",
        aspectRatio: "1 / 1",
        borderTopLeftRadius: 3,
        borderTopRightRadius: 3,
        borderBottom: "1px solid #e3e3e3",
        position: "relative",
        "& img": {
            width: "100%",
            minHeight: "100%",
            height: "auto",
            borderTopLeftRadius: 3,
            borderTopRightRadius: 3,
            minWidth: "85px",
        }
    },
    cardItemLink: {
        lineHeight: "16px",
        width: "100%",
        padding: "0 6px",
        paddingTop: 6,
        display: "inline-block",
        "& span": {
            height: "20px",
            lineHeight: "16px",
            display: "inline-block",
            maxWidth: '100%',
            fontSize: 14,
            padding: 0,
        },
        "@media(min-width: 992px)": {
            paddingTop: 6,
        }
    },
    cardItemLinkHeight: {
        height: 50,
    },
    cardEquipped: {
        borderRadius: 3,
        pointerEvents: "none",
        border: "2px solid #02b757",
        position: "absolute",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
    },
    restrictionsContainer: {
        position: 'absolute',
        bottom: -1,
        left: -2,
        overflow: 'hidden',
    },
    creatorContainer: {
        padding: '0!important',
        "& *": {
            fontSize: 10,
            fontWeight: 500,
        },
    },
    text: {
        padding: "0 5px",
    },
    currencyIcon: {
        marginTop: 1,
        marginRight: 2,
    },
    currencyText: { fontWeight: 500 },
    currencyTextWas: {
        fontWeight: 500,
        textDecoration: "line-through",
        color: "#757575",
        fontSize: 12,
    },

    itemStatusContainer: {
        position: 'absolute',
        top: 0,
        right: 0,
        margin: 6,
        display: 'flex',
        gap: 4
    },
    itemStatusSale: {
        display: 'inline-block',
        fontSize: 0,
        fontWeight: 500,
        backgroundColor: "#F23515",
        color: "#fff",
        padding: 3,
        borderRadius: 3,
        lineHeight: "1em",
    },
    itemStatusNew: {
        backgroundColor: "#FF8D00",
        padding: "6px 5px",
    },
    itemStatusSaleIcon: {
        backgroundPosition: "-16px -576px!important",
        width: 16,
        height: 16,
        backgroundSize: "32px auto",
    },
    itemStatusSaleNew: {
        fontSize: 10,
        lineHeight: "1em",
        fontWeight: 500,
        marginLeft: 2,
        color: "#fff",
    },
    itemStatusSaleBadge: {
        backgroundColor: "#E2231A",
        padding: "4px 7px",
        fontSize: 11,
        fontWeight: 600,
        color: "#fff",
        borderRadius: 3,
        lineHeight: "1em",
    },
    salesCounter: {
        fontWeight: 500,
        fontSize: 12,
        color: "#757575",
        marginRight: 6,
        lineHeight: "16px",
    },
    salesCounterValue: {
        fontWeight: 500,
        fontSize: 14,
        color: "var(--text-color-primary)",
        lineHeight: "16px",
    },
    saleClockContainer: {
        fontSize: 14,
        color: "var(--text-color-primary)",
        textAlign: "center",
        fontWeight: 300,
        lineHeight: '1.4em',
        // marginTop: 12,
        position: "absolute",
        width: 180,
    },
    saleClock: {
        fontSize: 14,
        color: "var(--text-color-primary)",
        textAlign: "center",
        fontWeight: 300,
        lineHeight: '1.4em',
        "& span": {
            color: "#E2231A",
            marginLeft: 3,
        },
    },
    specialElementsContainer: {
        overflow: 'hidden',
        display: 'flex',
        height: 20,
        padding: '0 6px'
    },
    noHeight: {
        height: 'auto!important',
        lineHeight: '1.4em',
    },
    remainingContainer: {
        color: "var(--bad-color)",
        fontWeight: 500,
        fontSize: 12,
    },
});

/**
 * @param {{item: CatalogAssetDetails;}} props
 * @returns
 */
function CatalogItemCard(props) {
    const { item } = props;
    const s = useStyles({theme: getTheme()});
    const [isNew, setNew] = useState(false);
    const [goingOffSale, setGoingOffSale] = useState(false);
    const [longName, setLongName] = useState(true);
    const [limited, setLimited] = useState(true);

    useEffect(() => {
        if (item.createdAt)
            setNew(IsISOWithinDays(item.createdAt, 3));
        if (item.offsaleDeadline)
            setGoingOffSale(new Date() < new Date(item.offsaleDeadline))
        setLimited((item.itemRestrictions.includes("Limited") || item.itemRestrictions.includes("LimitedUnique")));
    }, [item]);

    useEffect(() => {
        setLongName(!IsValidNum(item.unitsAvailableForConsumption) && (IsNullOrEmpty(item.creatorName) || item.creatorName.toLowerCase() === "roblox" || item.creatorName.toLowerCase() === "ugc") && !IsValidNum(item.lowestPrice))
    }, [item.creatorName, item.lowestPrice]);

    return <div className={`${s.cardWrapper}`}>
        <Link href={`/catalog/${item.id}/${encodeURIComponent(item.name)}`}>
            <a className={`${s.cardContainer}`} href={`/catalog/${item.id}/${encodeURIComponent(item.name)}`}>
                <div className={s.cardImage}>
                    <img src={ThumbnailFromState(item.imageUrl, item.state)} alt={item.name}/>
                    <div className={s.itemStatusContainer}>
                        {
                            item.isOnSale
                                ?
                                <div className={s.itemStatusSaleBadge}>Sale</div>
                                :
                                null
                        }
                        {
                            isNew && !goingOffSale
                                ?
                                <div className={`${s.itemStatusSale} ${s.itemStatusNew}`}>
                                    <span className={s.itemStatusSaleNew} style={{margin: 0}}>New</span>
                                </div>
                                :
                                null
                        }
                        {
                            isNew && goingOffSale
                                ?
                                <div className={`${s.itemStatusSale}`} style={{ display: 'flex', alignItems: 'center' }}>
                                    <span className={`${s.itemStatusSaleIcon} icon-clock`}/>
                                    <span className={s.itemStatusSaleNew}>New</span>
                                </div>
                                :
                                null
                        }
                        {
                            goingOffSale || IsValidNum(item.unitsAvailableForConsumption) || (item.isForSale && item.itemRestrictions.includes("Limited"))
                                ?
                                <div className={s.itemStatusSale}>
                                    <span className={`${s.itemStatusSaleIcon} icon-clock`}/>
                                </div>
                                :
                                null
                        }
                    </div>
                    <div className={s.restrictionsContainer}>
                        {
                            item?.itemRestrictions?.includes("LimitedUnique")
                            ?
                            <span className="icon-labels-18 LimitedUnique"/>
                            :
                            item?.itemRestrictions?.includes("Limited")
                            ?
                            <span className="icon-labels-18 Limited"/>
                            :
                            null
                        }
                    </div>
                </div>
                <a className={`${s.cardItemLink} ${longName ? s.cardItemLinkHeight : ''}`}
                   href={`/catalog/${item.id}/${encodeURIComponent(item.name)}`}>
                    <span className={
                        longName
                            ? `text-overflow-2 ${s.noHeight}` : 'text-overflow'} title={item.name}>{item.name}</span>
                </a>
                {
                    !longName && <div className={s.specialElementsContainer} style={IsValidNum(item.unitsAvailableForConsumption) ? {alignItems: "center"} : {}}>
                        {
                            !IsNullOrEmpty(item.creatorName) && item.creatorName.toLowerCase() !== "roblox" && item.creatorName.toLowerCase() !== "ugc"
                                ?
                                <div className={`${s.creatorContainer} ${s.text} flex w-fit-content`}>
                                    <span style={{ marginRight: 3, color: "var(--text-color-secondary)" }}>By</span>
                                    <CreatorLink id={item.creatorTargetId} type={item.creatorType}
                                                 name={item.creatorName}/>
                                </div>
                                :
                                IsValidNum(item.unitsAvailableForConsumption)
                                ?
                                    <span className={`flex ${s.remainingContainer}`}>{item.unitsAvailableForConsumption} remaining</span>
                                :
                                IsValidNum(item.lowestPrice)
                                    ?
                                    <div style={{fontWeight: 500, fontSize: 12, color: "#757575", lineHeight: 1.3}}>
                                        Was<span className={`${s.currencyIcon} icon-${item.priceTickets ? "tix" : "robux"}-gray-12x12`} style={{marginTop: -2, marginLeft: 2}}/>
                                        <span
                                            className={`${s.currencyTextWas} text-${item.priceTickets ? "tix" : "robux"}`}>{abbreviateNumber(item.priceTickets || item.price)}</span>
                                    </div>
                                    :
                                    null
                        }
                    </div>
                }
                <div className={`${s.text} flex w-fit-content`} style={{marginBottom: 5,}}>
                    {
                        item.isOnSale
                        ?
                        <div style={{display: 'flex', alignItems: 'baseline'}}>
                            <span className={s.salesCounter}>Sales</span>
                            <span className={s.salesCounterValue}>{abbreviateNumber(item.saleCount || 0)}</span>
                        </div>
                        :
                        !item.isForSale && !limited
                        ?
                        <span className={`${s.currencyText} text-free`}>Offsale</span>
                        :
                        !item.isForSale && !item.offsaleDeadline && !IsValidNum(item.unitsAvailableForConsumption) && item.priceStatus && item.priceStatus.toLowerCase().includes("no resellers")
                        ?
                        <span className={`${s.currencyText} text-free`}>{item.priceStatus}</span>
                        :
                        item.lowestPrice && !limited || item.price
                        ?
                        <>
                            <span className={`${s.currencyIcon} icon-robux-16x16`}/>
                            <span
                                className={`${s.currencyText} text-robux`}>{abbreviateNumber(item.lowestPrice || item.price)}</span>
                        </>
                        :
                        item.priceTickets
                        ?
                        <>
                            <span className={`${s.currencyIcon} icon-tix-16x16`}/>
                            <span
                                className={`${s.currencyText} text-tix`}>{abbreviateNumber(item.priceTickets)}</span>
                        </>
                        :
                        <span className={`${s.currencyText} text-free`}>Free</span>
                    }
                </div>
                {
                    item.owned ? <div className={s.cardEquipped} /> : null
                }
            </a>
        </Link>
    </div>
}

export default CatalogItemCard;
