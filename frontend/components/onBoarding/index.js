import {createUseStyles} from "react-jss";
import ActionButton from "../actionButton";
import useButtonStyles from "../../styles/buttonStyles";
import OnBoardingStore from "./store";

const useStyles = createUseStyles({
    bg: {
        backgroundImage: 'url(/img/play-averia-bg.png)',
        backgroundSize: '100% auto',
        backgroundRepeat: 'no-repeat',
        backgroundPosition: 'center',
        width: '100%',
        aspectRatio: '31 / 7',
        verticalAlign: 'middle',
        display: 'inline-block',
    },
    bgGradient: {
        background: "linear-gradient(to bottom, rgba(0, 0, 0, 0), var(--white-color))",
        height: '50%',
        bottom: '0'
    },
    beginBtn: {
        padding: "5px 20px",
    },
});

function OnBoarding() {
    const s = useStyles();
    const buttonStyles = useButtonStyles();
    const store = OnBoardingStore.useContainer();
    
    return <div className={`container col-12`}>
        <div className={`section-content padding-none`} style={{ borderRadius: 5, overflow: 'hidden' }}>
            <div className={`col-12`}>
                <div className={`col-12 position-relative`}>
                    <div className={`${s.bg}`} />
                    <div className={`${s.bgGradient} position-absolute w-100`} />
                </div>
            </div>
            <div className={`col-12 padding-15 pb-0 mt-1`}>
                <h1 className={`text-center`} style={{fontSize: 64}}>Welcome to Averia!</h1>
                <h5 className={`text-center`}>
                    We're happy to have you here. Click Begin to configure your settings.
                </h5>
            </div>
            <div className={`col-12 padding-15 pt-0 mt-5`}>
                <ActionButton
                    buttonStyle={buttonStyles.newBuyButton}
                    label="Begin"
                    onClick={() => {
                        store.setPage(2);
                    }}
                    className={s.beginBtn}
                />
            </div>
        </div>
    </div>
}

export default OnBoarding;
