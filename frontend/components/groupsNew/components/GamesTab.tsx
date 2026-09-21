import Section from "./Section";
import {createUseStyles} from "react-jss";
import {getTheme, themeType } from "../../../services/theme";

const useStyles = createUseStyles({
    headerContainerThemed: {
        "& h3": {
            color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
        },
    }
})

const GamesTab = ({}: {}) => {
    const s = useStyles({theme: getTheme()});
    return <div>
        <Section header={"Games"} headerContainer={s.headerContainerThemed} contentSectioned={true} className={"disabled"}>
            This group has not created any games yet.
        </Section>
    </div>
};

export default GamesTab;